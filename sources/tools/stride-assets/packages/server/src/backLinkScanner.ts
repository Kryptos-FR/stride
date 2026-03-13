import * as fs from 'fs';
import * as path from 'path';
import * as chokidar from 'chokidar';
import { AssetIndex } from './assetIndex';
import { findAssetReferences, findPartReferences, parseAssetHeader } from 'stride-assets-core';

// Extracts the YAML key context for a reference on a given line
function getLineContext(line: string): string {
    // Try to extract the YAML key (e.g., "Material:", "Texture:", "Active:")
    const keyMatch = /^\s*(?:[0-9a-fA-F]+~)?(\w[\w\s]*?):\s/.exec(line);
    if (keyMatch) {
        return keyMatch[1].trim();
    }
    // For ref!! lines, try the parent key
    if (line.includes('ref!!')) {
        return 'ref!!';
    }
    return '';
}

export class BackLinkScanner {
    private scanning = false;
    private watcher: chokidar.FSWatcher | undefined;

    constructor(
        private index: AssetIndex,
        private workspaceFolders: string[],
    ) {}

    async scanWorkspace(onProgress?: (msg: string) => void): Promise<void> {
        if (this.scanning) {
            return;
        }
        this.scanning = true;

        try {
            const files: string[] = [];
            for (const folder of this.workspaceFolders) {
                const folderFiles = await this.findSdFiles(folder);
                files.push(...folderFiles);
            }

            const total = files.length;
            let processed = 0;

            for (const filePath of files) {
                await this.scanFile(filePath);
                processed++;
                if (processed % 20 === 0) {
                    onProgress?.(`Scanning back-links: ${processed}/${total}...`);
                }
            }
        } finally {
            this.scanning = false;
        }
    }

    async scanFile(filePath: string): Promise<void> {
        try {
            const content = await fs.promises.readFile(filePath, 'utf-8');
            const header = parseAssetHeader(content);
            if (!header) {
                return;
            }

            // Clear old back-refs from this file before re-scanning
            this.index.clearBackRefsFromFile(filePath);

            const lines = content.split('\n');

            // Scan for asset references (GUID:Name)
            const assetRefs = findAssetReferences(content);
            for (const ref of assetRefs) {
                // Skip the asset's own Id
                if (ref.guid === header.id) {
                    continue;
                }
                const context = ref.line < lines.length ? getLineContext(lines[ref.line]) : '';
                this.index.addBackRef(ref.guid, {
                    sourceFilePath: filePath,
                    line: ref.line,
                    column: ref.startColumn,
                    context,
                });
            }

            // Scan for part references (ref!! GUID)
            const partRefs = findPartReferences(content);
            for (const ref of partRefs) {
                const context = ref.line < lines.length ? getLineContext(lines[ref.line]) : 'ref!!';
                this.index.addBackRef(ref.guid, {
                    sourceFilePath: filePath,
                    line: ref.line,
                    column: ref.startColumn,
                    context,
                });
            }
        } catch {
            // Skip unreadable files
        }
    }

    setupFileWatcher(): void {
        const patterns = this.workspaceFolders.map(f => path.join(f, '**/*.sd*'));
        this.watcher = chokidar.watch(patterns, {
            ignoreInitial: true,
            ignored: ['**/bin/**', '**/obj/**', '**/.git/**'],
        });

        this.watcher.on('change', async (filePath) => {
            await this.scanFile(filePath);
        });

        this.watcher.on('add', async (filePath) => {
            await this.scanFile(filePath);
        });

        this.watcher.on('unlink', (filePath) => {
            this.index.clearBackRefsFromFile(filePath);
        });
    }

    private async findSdFiles(root: string): Promise<string[]> {
        const results: string[] = [];
        const walk = async (dir: string) => {
            try {
                const entries = await fs.promises.readdir(dir, { withFileTypes: true });
                for (const entry of entries) {
                    const fullPath = path.join(dir, entry.name);
                    if (entry.isDirectory()) {
                        if (entry.name === 'bin' || entry.name === 'obj' || entry.name === '.git') {
                            continue;
                        }
                        await walk(fullPath);
                    } else if (entry.isFile() && /\.sd\w+$/i.test(entry.name)) {
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

    dispose(): void {
        this.watcher?.close();
    }
}
