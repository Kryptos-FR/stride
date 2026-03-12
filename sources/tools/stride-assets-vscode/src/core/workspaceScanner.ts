import * as vscode from 'vscode';
import * as fs from 'fs';
import * as path from 'path';
import { AssetIndex } from './assetIndex';
import { parseAssetHeader, headerToAssetEntry, parseAssetFull } from './assetParser';
import { parsePackageFile, PackageInfo } from './packageParser';

export class WorkspaceScanner implements vscode.Disposable {
    private watchers: vscode.FileSystemWatcher[] = [];
    private packages: PackageInfo[] = [];

    constructor(private index: AssetIndex) {}

    async initialize(progress?: vscode.Progress<{ message?: string; increment?: number }>): Promise<void> {
        this.packages = await this.findPackages();
        progress?.report({ message: 'Found packages, scanning assets...' });

        if (this.packages.length > 0) {
            await this.scanPackageFolders(progress);
        } else {
            await this.scanWorkspace(progress);
        }

        this.setupWatchers();
    }

    private async findPackages(): Promise<PackageInfo[]> {
        const pkgFiles = await vscode.workspace.findFiles('**/*.sdpkg', '**/bin/**');
        const packages: PackageInfo[] = [];

        for (const uri of pkgFiles) {
            try {
                const content = await fs.promises.readFile(uri.fsPath, 'utf-8');
                const pkg = parsePackageFile(content, uri.fsPath);
                packages.push(pkg);
            } catch {
                // Skip unreadable package files
            }
        }

        return packages;
    }

    private async scanPackageFolders(progress?: vscode.Progress<{ message?: string; increment?: number }>): Promise<void> {
        const allFolders = new Set<string>();
        for (const pkg of this.packages) {
            for (const folder of pkg.assetFolders) {
                allFolders.add(folder);
            }
        }

        let scanned = 0;
        const total = allFolders.size;

        for (const folder of allFolders) {
            progress?.report({ message: `Scanning ${path.basename(folder)}...`, increment: (1 / total) * 100 });
            await this.scanFolder(folder);
            scanned++;
        }
    }

    private async scanWorkspace(progress?: vscode.Progress<{ message?: string; increment?: number }>): Promise<void> {
        progress?.report({ message: 'Scanning workspace for asset files...' });
        const files = await vscode.workspace.findFiles('**/*.sd*', '{**/bin/**,**/obj/**,**/.git/**}');

        const total = files.length;
        let processed = 0;

        for (const uri of files) {
            await this.indexFile(uri.fsPath);
            processed++;
            if (processed % 50 === 0) {
                progress?.report({ message: `Indexed ${processed}/${total} assets...` });
            }
        }
    }

    private async scanFolder(folderPath: string): Promise<void> {
        try {
            const stat = await fs.promises.stat(folderPath);
            if (!stat.isDirectory()) {
                return;
            }
        } catch {
            return; // Folder doesn't exist
        }

        // Use VS Code's findFiles scoped to this folder
        const workspaceFolders = vscode.workspace.workspaceFolders;
        if (!workspaceFolders) {
            return;
        }

        // Find all .sd* files in the folder recursively
        const entries = await this.walkDirectory(folderPath);
        for (const filePath of entries) {
            if (/\.sd[a-z]+$/i.test(filePath)) {
                await this.indexFile(filePath);
            }
        }
    }

    private async walkDirectory(dir: string): Promise<string[]> {
        const results: string[] = [];
        try {
            const entries = await fs.promises.readdir(dir, { withFileTypes: true });
            for (const entry of entries) {
                const fullPath = path.join(dir, entry.name);
                if (entry.isDirectory()) {
                    const subResults = await this.walkDirectory(fullPath);
                    results.push(...subResults);
                } else if (entry.isFile()) {
                    results.push(fullPath);
                }
            }
        } catch {
            // Skip unreadable directories
        }
        return results;
    }

    private async indexFile(filePath: string): Promise<void> {
        try {
            // Read only the first 500 bytes for header parsing
            const fd = await fs.promises.open(filePath, 'r');
            const buffer = Buffer.alloc(500);
            const { bytesRead } = await fd.read(buffer, 0, 500, 0);
            await fd.close();

            const content = buffer.toString('utf-8', 0, bytesRead);
            const header = parseAssetHeader(content);
            if (header) {
                const entry = headerToAssetEntry(header, filePath);
                this.index.addAsset(entry);
            }
        } catch {
            // Skip unreadable files
        }
    }

    // Re-index a single file (full content, used on file change for open files)
    async reindexFile(filePath: string): Promise<void> {
        try {
            const content = await fs.promises.readFile(filePath, 'utf-8');
            const header = parseAssetHeader(content);
            if (header) {
                const entry = headerToAssetEntry(header, filePath);
                this.index.updateFile(filePath, [entry]);

                // If it's a scene/prefab, also index entities
                if (filePath.endsWith('.sdscene') || filePath.endsWith('.sdprefab')) {
                    const parsed = parseAssetFull(content);
                    if (parsed) {
                        this.index.clearEntitiesForFile(filePath);
                        for (const entity of parsed.entities) {
                            this.index.addEntity({
                                id: entity.id,
                                filePath,
                                name: entity.name,
                                line: entity.line,
                            });
                        }
                    }
                }
            }
        } catch {
            // Skip unreadable files
        }
    }

    private setupWatchers(): void {
        const watcher = vscode.workspace.createFileSystemWatcher('**/*.sd*');

        watcher.onDidCreate(async (uri) => {
            await this.indexFile(uri.fsPath);
        });

        watcher.onDidChange(async (uri) => {
            await this.reindexFile(uri.fsPath);
        });

        watcher.onDidDelete((uri) => {
            this.index.removeFile(uri.fsPath);
        });

        this.watchers.push(watcher);
    }

    getPackages(): PackageInfo[] {
        return this.packages;
    }

    dispose(): void {
        for (const watcher of this.watchers) {
            watcher.dispose();
        }
        this.watchers = [];
    }
}
