import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { AssetIndex } from '../core/assetIndex';
import { findAssetReferences, findSourcePaths, findScriptReferences } from '../core/referencePattern';

export class StrideDiagnosticsProvider implements vscode.Disposable {
    private diagnosticCollection: vscode.DiagnosticCollection;
    private disposables: vscode.Disposable[] = [];
    private debounceTimers = new Map<string, NodeJS.Timeout>();
    // Tracks whether the workspace symbol provider has returned any results yet.
    // Until it does, we skip script diagnostics to avoid false negatives while
    // the C# language server (OmniSharp or Roslyn) is still loading.
    private symbolProviderReady = false;
    private pendingRetry: NodeJS.Timeout | undefined;

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

    private scheduleRetry(): void {
        // Schedule a re-check of visible editors after a delay.
        // This handles the case where the symbol provider wasn't ready on first attempt.
        if (this.pendingRetry) {
            return; // Already scheduled
        }
        this.pendingRetry = setTimeout(() => {
            this.pendingRetry = undefined;
            for (const editor of vscode.window.visibleTextEditors) {
                this.scheduleUpdate(editor.document);
            }
        }, 5000);
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

        this.debounceTimers.set(key, setTimeout(async () => {
            await this.updateDiagnostics(document);
            this.debounceTimers.delete(key);
        }, 300));
    }

    private isStrideAsset(document: vscode.TextDocument): boolean {
        return document.languageId === 'stride-asset';
    }

    private async updateDiagnostics(document: vscode.TextDocument): Promise<void> {
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

        // Check script references (only if enabled and only for local projects)
        const scriptNavigationEnabled = vscode.workspace.getConfiguration('strideAssets').get<boolean>('scriptNavigationEnabled', false);
        if (scriptNavigationEnabled) {
            const scriptRefs = findScriptReferences(text);
            const localRefs = scriptRefs.filter(r => this.index.hasProject(r.assemblyName));

            if (localRefs.length > 0) {
                // Query workspace symbols for each local script reference
                const lookupResults: { ref: typeof localRefs[0]; found: boolean }[] = [];
                let anySymbolsReturned = false;

                for (const ref of localRefs) {
                    const symbols = await vscode.commands.executeCommand<vscode.SymbolInformation[]>(
                        'vscode.executeWorkspaceSymbolProvider',
                        ref.typeName
                    );
                    const hasResults = (symbols?.length ?? 0) > 0;
                    if (hasResults) {
                        anySymbolsReturned = true;
                    }
                    const className = ref.typeName.split('.').pop();
                    const found = symbols?.some(s =>
                        s.kind === vscode.SymbolKind.Class && s.name === className
                    ) ?? false;
                    lookupResults.push({ ref, found });
                }

                if (anySymbolsReturned || this.symbolProviderReady) {
                    // Provider is working — report genuine missing types
                    if (anySymbolsReturned) {
                        this.symbolProviderReady = true;
                    }
                    for (const { ref, found } of lookupResults) {
                        if (!found) {
                            const range = new vscode.Range(
                                new vscode.Position(ref.line, ref.startColumn),
                                new vscode.Position(ref.line, ref.endColumn)
                            );
                            const diag = new vscode.Diagnostic(
                                range,
                                `Script type not found: ${ref.typeName} (${ref.assemblyName})`,
                                vscode.DiagnosticSeverity.Warning
                            );
                            diag.source = 'Stride Assets';
                            diagnostics.push(diag);
                        }
                    }
                } else {
                    // Provider returned nothing for all queries — likely still loading.
                    // Schedule a retry to re-check once it's ready.
                    this.scheduleRetry();
                }
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
                await this.updateDiagnostics(doc);
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
        if (this.pendingRetry) {
            clearTimeout(this.pendingRetry);
        }
    }
}
