import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { AssetIndex } from '../core/assetIndex';
import { findAssetReferences, findSourcePaths } from '../core/referencePattern';

export class StrideDiagnosticsProvider implements vscode.Disposable {
    private diagnosticCollection: vscode.DiagnosticCollection;
    private disposables: vscode.Disposable[] = [];
    private debounceTimers = new Map<string, NodeJS.Timeout>();

    constructor(private index: AssetIndex) {
        this.diagnosticCollection = vscode.languages.createDiagnosticCollection('stride-assets');

        // Update diagnostics when a document is opened or changed
        this.disposables.push(
            vscode.workspace.onDidOpenTextDocument((doc) => this.scheduleUpdate(doc)),
            vscode.workspace.onDidChangeTextDocument((e) => this.scheduleUpdate(e.document)),
            vscode.workspace.onDidCloseTextDocument((doc) => {
                this.diagnosticCollection.delete(doc.uri);
                this.clearTimer(doc.uri.toString());
            }),
            // Re-check open documents when the index updates
            index.onDidUpdate(() => {
                for (const editor of vscode.window.visibleTextEditors) {
                    this.scheduleUpdate(editor.document);
                }
            })
        );
    }

    private clearTimer(key: string): void {
        const timer = this.debounceTimers.get(key);
        if (timer) {
            clearTimeout(timer);
            this.debounceTimers.delete(key);
        }
    }

    private scheduleUpdate(document: vscode.TextDocument): void {
        if (!this.isStrideAsset(document)) {
            return;
        }

        const enabled = vscode.workspace.getConfiguration('strideAssets').get<boolean>('diagnosticsEnabled', true);
        if (!enabled) {
            return;
        }

        const key = document.uri.toString();
        this.clearTimer(key);

        this.debounceTimers.set(key, setTimeout(() => {
            this.updateDiagnostics(document);
            this.debounceTimers.delete(key);
        }, 300));
    }

    private isStrideAsset(document: vscode.TextDocument): boolean {
        return document.languageId === 'stride-asset';
    }

    private updateDiagnostics(document: vscode.TextDocument): void {
        const text = document.getText();
        const diagnostics: vscode.Diagnostic[] = [];

        // Check asset references
        const assetRefs = findAssetReferences(text);
        for (const ref of assetRefs) {
            // Skip the asset's own Id (line 1)
            if (ref.line <= 1) {
                continue;
            }

            const entry = this.index.lookupGuid(ref.guid);
            if (!entry) {
                const range = new vscode.Range(
                    new vscode.Position(ref.line, ref.startColumn),
                    new vscode.Position(ref.line, ref.endColumn)
                );
                const diag = new vscode.Diagnostic(
                    range,
                    `Asset not found: ${ref.name} (${ref.guid})`,
                    vscode.DiagnosticSeverity.Warning
                );
                diag.source = 'Stride Assets';
                diagnostics.push(diag);
            } else if (entry.location !== ref.name) {
                // Name mismatch (informational)
                const range = new vscode.Range(
                    new vscode.Position(ref.line, ref.startColumn),
                    new vscode.Position(ref.line, ref.endColumn)
                );
                const diag = new vscode.Diagnostic(
                    range,
                    `Asset name mismatch: file is "${entry.location}", reference says "${ref.name}"`,
                    vscode.DiagnosticSeverity.Information
                );
                diag.source = 'Stride Assets';
                diagnostics.push(diag);
            }
        }

        // Check source paths
        const sourcePaths = findSourcePaths(text);
        for (const sp of sourcePaths) {
            const dir = path.dirname(document.uri.fsPath);
            const resolved = path.resolve(dir, sp.path);
            if (!fs.existsSync(resolved)) {
                const range = new vscode.Range(
                    new vscode.Position(sp.line, sp.startColumn),
                    new vscode.Position(sp.line, sp.endColumn)
                );
                const diag = new vscode.Diagnostic(
                    range,
                    `Source file not found: ${sp.path}`,
                    vscode.DiagnosticSeverity.Error
                );
                diag.source = 'Stride Assets';
                diagnostics.push(diag);
            }
        }

        this.diagnosticCollection.set(document.uri, diagnostics);
    }

    // Scan all asset files in workspace for broken references (optional, triggered by setting)
    async scanWorkspace(): Promise<void> {
        const files = await vscode.workspace.findFiles('**/*.sd*', '{**/bin/**,**/obj/**,**/.git/**}');
        for (const uri of files) {
            try {
                const doc = await vscode.workspace.openTextDocument(uri);
                this.updateDiagnostics(doc);
            } catch {
                // Skip unreadable files
            }
        }
    }

    dispose(): void {
        this.diagnosticCollection.dispose();
        for (const d of this.disposables) {
            d.dispose();
        }
        for (const timer of this.debounceTimers.values()) {
            clearTimeout(timer);
        }
        this.debounceTimers.clear();
    }
}
