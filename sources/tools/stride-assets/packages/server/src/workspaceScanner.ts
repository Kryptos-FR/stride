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
    private logFn: ((msg: string) => void) | undefined;

    constructor(
        private index: AssetIndex,
        private workspaceFolders: string[],
    ) {}

    setLogger(fn: (msg: string) => void): void {
        this.logFn = fn;
    }

    private log(msg: string): void {
        this.logFn?.(msg);
    }

    async initialize(onProgress?: (progress: ScanProgress) => void): Promise<void> {
        this.log(`Workspace folders: ${this.workspaceFolders.join(', ')}`);
        this.packages = await this.findPackages();
        this.log(`Found ${this.packages.length} package file(s)`);
        for (const pkg of this.packages) {
            this.log(`  package "${pkg.name}": ${pkg.assetFolders.length} asset folder(s)`);
            for (const folder of pkg.assetFolders) {
                this.log(`    folder: ${folder}`);
            }
        }
        onProgress?.({ message: `Found ${this.packages.length} packages, scanning assets...` });

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
            this.log(`Searching for .sdpkg files in: ${folder}`);
            const pkgFiles = await this.findFiles(folder, /\.sdpkg$/i);
            this.log(`  found ${pkgFiles.length} .sdpkg file(s)`);
            for (const pkgPath of pkgFiles) {
                try {
                    const content = await fs.promises.readFile(pkgPath, 'utf-8');
                    const pkg = parsePackageFile(content, pkgPath);
                    this.log(`  parsed package: ${pkgPath} -> "${pkg.name}"`);
                    packages.push(pkg);
                } catch (err) {
                    this.log(`  failed to parse: ${pkgPath}: ${err}`);
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

        this.log(`Scanning ${allFolders.size} asset folder(s)`);
        const total = allFolders.size;
        for (const folder of allFolders) {
            onProgress?.({ message: `Scanning ${path.basename(folder)}...`, increment: (1 / total) * 100 });
            await this.scanFolder(folder);
        }
    }

    private async scanWorkspace(onProgress?: (progress: ScanProgress) => void): Promise<void> {
        onProgress?.({ message: 'Scanning workspace for asset files...' });

        for (const folder of this.workspaceFolders) {
            this.log(`Scanning workspace folder: ${folder}`);
            const files = await this.findFiles(folder, /\.sd\w+$/i);
            this.log(`  found ${files.length} .sd* file(s)`);
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
                this.log(`  not a directory: ${folderPath}`);
                return;
            }
        } catch {
            this.log(`  folder does not exist: ${folderPath}`);
            return;
        }

        const entries = await this.walkDirectory(folderPath);
        let indexed = 0;
        for (const filePath of entries) {
            if (/\.sd\w+$/i.test(filePath)) {
                await this.indexFile(filePath);
                indexed++;
            }
        }
        this.log(`  indexed ${indexed} assets from ${folderPath}`);
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
        this.log(`Re-indexing: ${filePath}`);
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
                        this.log(`  re-indexed ${parsed.parts.length} parts`);
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
                this.log(`Indexed ${parsed.parts.length} parts from open document: ${path.basename(filePath)}`);
            }
        }
    }

    private setupWatcher(): void {
        // Watch all workspace folders for .sd* file changes
        const patterns = this.workspaceFolders.map(f => path.join(f, '**/*.sd*'));
        this.log(`Setting up file watcher for: ${patterns.join(', ')}`);
        this.watcher = chokidar.watch(patterns, {
            ignoreInitial: true,
            ignored: ['**/bin/**', '**/obj/**', '**/.git/**'],
        });

        this.watcher.on('add', async (filePath) => {
            this.log(`File added: ${filePath}`);
            await this.indexFile(filePath);
        });

        this.watcher.on('change', async (filePath) => {
            this.log(`File changed: ${filePath}`);
            await this.reindexFile(filePath);
        });

        this.watcher.on('unlink', (filePath) => {
            this.log(`File deleted: ${filePath}`);
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
        this.log(`Discovered ${names.size} .csproj project(s): ${Array.from(names).join(', ')}`);
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
