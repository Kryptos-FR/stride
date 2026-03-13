import * as fs from 'fs';
import * as path from 'path';
import * as chokidar from 'chokidar';
import { AssetIndex } from './assetIndex';
import { parseAssetHeader, headerToAssetEntry, parseAssetFull, parsePackageFile, PackageInfo } from 'stride-assets-core';

export interface ScanProgress {
    message: string;
    increment?: number;
}

export class WorkspaceScanner {
    private watcher: chokidar.FSWatcher | undefined;
    private packages: PackageInfo[] = [];

    constructor(
        private index: AssetIndex,
        private workspaceFolders: string[],
    ) {}

    async initialize(onProgress?: (progress: ScanProgress) => void): Promise<void> {
        this.packages = await this.findPackages();
        onProgress?.({ message: 'Found packages, scanning assets...' });

        if (this.packages.length > 0) {
            await this.scanPackageFolders(onProgress);
        } else {
            await this.scanWorkspace(onProgress);
        }

        // Discover .csproj project names for script reference validation
        await this.discoverProjects();

        this.setupWatcher();
    }

    private async findPackages(): Promise<PackageInfo[]> {
        const packages: PackageInfo[] = [];

        for (const folder of this.workspaceFolders) {
            const pkgFiles = await this.findFiles(folder, /\.sdpkg$/i);
            for (const pkgPath of pkgFiles) {
                try {
                    const content = await fs.promises.readFile(pkgPath, 'utf-8');
                    const pkg = parsePackageFile(content, pkgPath);
                    packages.push(pkg);
                } catch {
                    // Skip unreadable package files
                }
            }
        }

        return packages;
    }

    private async scanPackageFolders(onProgress?: (progress: ScanProgress) => void): Promise<void> {
        const allFolders = new Set<string>();
        for (const pkg of this.packages) {
            for (const folder of pkg.assetFolders) {
                allFolders.add(folder);
            }
        }

        const total = allFolders.size;
        for (const folder of allFolders) {
            onProgress?.({ message: `Scanning ${path.basename(folder)}...`, increment: (1 / total) * 100 });
            await this.scanFolder(folder);
        }
    }

    private async scanWorkspace(onProgress?: (progress: ScanProgress) => void): Promise<void> {
        onProgress?.({ message: 'Scanning workspace for asset files...' });

        for (const folder of this.workspaceFolders) {
            const files = await this.findFiles(folder, /\.sd\w+$/i);
            const total = files.length;
            let processed = 0;

            for (const filePath of files) {
                await this.indexFile(filePath);
                processed++;
                if (processed % 50 === 0) {
                    onProgress?.({ message: `Indexed ${processed}/${total} assets...` });
                }
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

        const entries = await this.walkDirectory(folderPath);
        for (const filePath of entries) {
            if (/\.sd\w+$/i.test(filePath)) {
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
                    // Skip bin, obj, .git directories
                    if (entry.name === 'bin' || entry.name === 'obj' || entry.name === '.git') {
                        continue;
                    }
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

    // Re-index a single file (full content, used on file change)
    async reindexFile(filePath: string): Promise<void> {
        try {
            const content = await fs.promises.readFile(filePath, 'utf-8');
            const header = parseAssetHeader(content);
            if (header) {
                const entry = headerToAssetEntry(header, filePath);
                this.index.updateFile(filePath, [entry]);

                // If it's a composite asset, also index parts (entities, UI elements, etc.)
                if (filePath.endsWith('.sdscene') || filePath.endsWith('.sdprefab') || filePath.endsWith('.sduipage') || filePath.endsWith('.sduilib')) {
                    const parsed = parseAssetFull(content);
                    if (parsed) {
                        this.index.clearPartsForFile(filePath);
                        for (const part of parsed.parts) {
                            this.index.addPart({
                                id: part.id,
                                filePath,
                                name: part.name,
                                line: part.line,
                            });
                        }
                    }
                }
            }
        } catch {
            // Skip unreadable files
        }
    }

    // Index parts for a document given its content (used when files are opened)
    indexPartsFromContent(filePath: string, content: string): void {
        if (filePath.endsWith('.sdscene') || filePath.endsWith('.sdprefab') || filePath.endsWith('.sduipage') || filePath.endsWith('.sduilib')) {
            const parsed = parseAssetFull(content);
            if (parsed) {
                this.index.clearPartsForFile(filePath);
                for (const part of parsed.parts) {
                    this.index.addPart({
                        id: part.id,
                        filePath,
                        name: part.name,
                        line: part.line,
                    });
                }
            }
        }
    }

    private setupWatcher(): void {
        // Watch all workspace folders for .sd* file changes
        const patterns = this.workspaceFolders.map(f => path.join(f, '**/*.sd*'));
        this.watcher = chokidar.watch(patterns, {
            ignoreInitial: true,
            ignored: ['**/bin/**', '**/obj/**', '**/.git/**'],
        });

        this.watcher.on('add', async (filePath) => {
            await this.indexFile(filePath);
        });

        this.watcher.on('change', async (filePath) => {
            await this.reindexFile(filePath);
        });

        this.watcher.on('unlink', (filePath) => {
            this.index.removeFile(filePath);
        });
    }

    private async discoverProjects(): Promise<void> {
        const names = new Set<string>();
        for (const folder of this.workspaceFolders) {
            const csprojFiles = await this.findFiles(folder, /\.csproj$/i);
            for (const csproj of csprojFiles) {
                names.add(path.basename(csproj, '.csproj'));
            }
        }
        this.index.setProjectNames(names);
    }

    private async findFiles(root: string, pattern: RegExp): Promise<string[]> {
        const results: string[] = [];
        const walk = async (dir: string) => {
            try {
                const entries = await fs.promises.readdir(dir, { withFileTypes: true });
                for (const entry of entries) {
                    const fullPath = path.join(dir, entry.name);
                    if (entry.isDirectory()) {
                        if (entry.name === 'bin' || entry.name === 'obj' || entry.name === '.git' || entry.name === 'node_modules') {
                            continue;
                        }
                        await walk(fullPath);
                    } else if (entry.isFile() && pattern.test(entry.name)) {
                        results.push(fullPath);
                    }
                }
            } catch {
                // Skip unreadable directories
            }
        };
        await walk(root);
        return results;
    }

    getPackages(): PackageInfo[] {
        return this.packages;
    }

    dispose(): void {
        this.watcher?.close();
    }
}
