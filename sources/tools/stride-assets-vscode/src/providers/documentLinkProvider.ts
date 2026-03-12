import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { AssetIndex } from '../core/assetIndex';
import { findAssetReferences, findSourcePaths } from '../core/referencePattern';

export class StrideDocumentLinkProvider implements vscode.DocumentLinkProvider {
    private outputChannel: vscode.OutputChannel;

    constructor(private index: AssetIndex) {
        this.outputChannel = vscode.window.createOutputChannel('Stride Assets');
    }

    provideDocumentLinks(
        document: vscode.TextDocument,
        _token: vscode.CancellationToken
    ): vscode.DocumentLink[] {
        const text = document.getText();
        const links: vscode.DocumentLink[] = [];

        this.outputChannel.appendLine(`[DocumentLinkProvider] Processing: ${document.uri.fsPath}`);

        // Asset references (GUID:Name) -> link to the asset file
        const assetRefs = findAssetReferences(text);
        this.outputChannel.appendLine(`  Found ${assetRefs.length} asset references`);
        for (const ref of assetRefs) {
            const entry = this.index.lookupGuid(ref.guid);
            if (entry) {
                const range = new vscode.Range(
                    new vscode.Position(ref.line, ref.startColumn),
                    new vscode.Position(ref.line, ref.endColumn)
                );
                const link = new vscode.DocumentLink(range, vscode.Uri.file(entry.filePath));
                link.tooltip = `Open ${entry.typeName}: ${entry.location}`;
                links.push(link);
            }
        }

        // Source paths -> file URI link (underlined + clickable)
        // The definition provider handles Ctrl+click to reveal in Explorer
        const sourcePaths = findSourcePaths(text);
        this.outputChannel.appendLine(`  Found ${sourcePaths.length} source paths`);
        for (const sp of sourcePaths) {
            const dir = path.dirname(document.uri.fsPath);
            const resolved = path.resolve(dir, sp.path);
            this.outputChannel.appendLine(`  Source: "${sp.path}" -> "${resolved}" (line ${sp.line}, col ${sp.startColumn}-${sp.endColumn})`);
            const range = new vscode.Range(
                new vscode.Position(sp.line, sp.startColumn),
                new vscode.Position(sp.line, sp.endColumn)
            );

            const uri = vscode.Uri.file(resolved);
            const link = new vscode.DocumentLink(range, uri);
            link.tooltip = fs.existsSync(resolved)
                ? `Open resource: ${path.basename(sp.path)}`
                : `Source file missing: ${sp.path}`;
            links.push(link);
        }

        this.outputChannel.appendLine(`  Total links: ${links.length}`);
        return links;
    }
}
