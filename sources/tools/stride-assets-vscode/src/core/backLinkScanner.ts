import * as vscode from 'vscode';
import * as fs from 'fs';
import * as path from 'path';
import { AssetIndex } from './assetIndex';
import { findAssetReferences, findPartReferences } from './referencePattern';
import { parseAssetHeader } from './assetParser';

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

export class BackLinkScanner implements vscode.Disposable {
    private scanning = false;
    private disposables: vscode.Disposable[] = [];

    constructor(private index: AssetIndex) {}

    async scanWorkspace(progress?: vscode.Progress<{ message?: string; increment?: number }>): Promise<void> {
        if (this.scanning) {
            return;
        }
        this.scanning = true;

        try {
            const files = await vscode.workspace.findFiles(
                '**/*.sd*',
                '{**/bin/**,**/obj/**,**/.git/**}'
            );

            const total = files.length;
            let processed = 0;

            for (const uri of files) {
                await this.scanFile(uri.fsPath);
                processed++;
                if (processed % 20 === 0) {
                    progress?.report({
                        message: `Scanning back-links: ${processed}/${total}...`,
                        increment: (20 / total) * 100,
                    });
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
        const watcher = vscode.workspace.createFileSystemWatcher('**/*.sd*');

        watcher.onDidChange(async (uri) => {
            await this.scanFile(uri.fsPath);
        });

        watcher.onDidCreate(async (uri) => {
            await this.scanFile(uri.fsPath);
        });

        watcher.onDidDelete((uri) => {
            this.index.clearBackRefsFromFile(uri.fsPath);
        });

        this.disposables.push(watcher);
    }

    dispose(): void {
        for (const d of this.disposables) {
            d.dispose();
        }
        this.disposables = [];
    }
}
