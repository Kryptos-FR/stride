import * as fs from 'fs';
import * as path from 'path';
import { Connection, Diagnostic, DiagnosticSeverity, Range, Position } from 'vscode-languageserver';
import { TextDocument } from 'vscode-languageserver-textdocument';
import { AssetIndex } from '../assetIndex';
import { findAssetReferences, findSourcePaths, findScriptReferences } from 'stride-assets-core';
import { ResolveCSharpSymbolRequest } from '../protocol';

interface Settings {
    diagnosticsEnabled: boolean;
    scriptNavigationEnabled: boolean;
}

export function createDiagnosticsHandler(
    index: AssetIndex,
    connection: Connection,
    getSettings: () => Settings,
) {
    const debounceTimers = new Map<string, NodeJS.Timeout>();
    const lastDiagnosticVersion = new Map<string, number>();
    let symbolProviderReady = false;
    let pendingRetry: NodeJS.Timeout | undefined;

    function clearTimer(key: string): void {
        const timer = debounceTimers.get(key);
        if (timer) {
            clearTimeout(timer);
            debounceTimers.delete(key);
        }
    }

    async function updateDiagnostics(doc: TextDocument): Promise<void> {
        const key = doc.uri;
        if (doc.version === lastDiagnosticVersion.get(key)) {
            return;
        }

        const settings = getSettings();
        if (!settings.diagnosticsEnabled) {
            return;
        }

        const text = doc.getText();
        const diagnostics: Diagnostic[] = [];
        const filePath = uriToPath(doc.uri);

        // Check asset references
        const assetRefs = findAssetReferences(text);
        for (const ref of assetRefs) {
            if (ref.line <= 1) { continue; }

            const entry = index.lookupGuid(ref.guid);
            if (!entry) {
                diagnostics.push({
                    range: Range.create(Position.create(ref.line, ref.startColumn), Position.create(ref.line, ref.endColumn)),
                    message: `Asset not found: ${ref.name} (${ref.guid})`,
                    severity: DiagnosticSeverity.Warning,
                    source: 'Stride Assets',
                });
            } else if (entry.location !== ref.name) {
                diagnostics.push({
                    range: Range.create(Position.create(ref.line, ref.startColumn), Position.create(ref.line, ref.endColumn)),
                    message: `Asset name mismatch: file is "${entry.location}", reference says "${ref.name}"`,
                    severity: DiagnosticSeverity.Information,
                    source: 'Stride Assets',
                });
            }
        }

        // Check source paths
        const sourcePaths = findSourcePaths(text);
        for (const sp of sourcePaths) {
            const dir = path.dirname(filePath);
            const resolved = path.resolve(dir, sp.path);
            let exists = true;
            try { await fs.promises.access(resolved); } catch { exists = false; }
            if (!exists) {
                diagnostics.push({
                    range: Range.create(Position.create(sp.line, sp.startColumn), Position.create(sp.line, sp.endColumn)),
                    message: `Source file not found: ${sp.path}`,
                    severity: DiagnosticSeverity.Error,
                    source: 'Stride Assets',
                });
            }
        }

        // Check script references (via custom request to client)
        if (settings.scriptNavigationEnabled) {
            const scriptRefs = findScriptReferences(text);
            const localRefs = scriptRefs.filter(r => index.hasProject(r.assemblyName));

            if (localRefs.length > 0) {
                const cache = new Map<string, boolean>();
                let anyResolved = false;

                for (const ref of localRefs) {
                    if (cache.has(ref.typeName)) {
                        if (cache.get(ref.typeName)) { anyResolved = true; }
                        continue;
                    }

                    try {
                        const result = await connection.sendRequest(ResolveCSharpSymbolRequest, {
                            typeName: ref.typeName,
                        });
                        const found = !!result.location;
                        cache.set(ref.typeName, found);
                        if (found) { anyResolved = true; }
                    } catch {
                        cache.set(ref.typeName, false);
                    }
                }

                if (anyResolved || symbolProviderReady) {
                    if (anyResolved) { symbolProviderReady = true; }
                    for (const ref of localRefs) {
                        if (!cache.get(ref.typeName)) {
                            diagnostics.push({
                                range: Range.create(Position.create(ref.line, ref.startColumn), Position.create(ref.line, ref.endColumn)),
                                message: `Script type not found: ${ref.typeName} (${ref.assemblyName})`,
                                severity: DiagnosticSeverity.Warning,
                                source: 'Stride Assets',
                            });
                        }
                    }
                } else {
                    scheduleRetry();
                }
            }
        }

        connection.sendDiagnostics({ uri: doc.uri, diagnostics });
        lastDiagnosticVersion.set(key, doc.version);
    }

    function scheduleRetry(): void {
        if (pendingRetry) { return; }
        pendingRetry = setTimeout(() => {
            pendingRetry = undefined;
            lastDiagnosticVersion.clear();
            // Note: the server will re-trigger diagnostics on next document event
        }, 5000);
    }

    return {
        onDocumentChange(doc: TextDocument): void {
            const key = doc.uri;
            clearTimer(key);
            debounceTimers.set(key, setTimeout(async () => {
                await updateDiagnostics(doc);
                debounceTimers.delete(key);
            }, 300));
        },

        onDocumentClose(uri: string): void {
            clearTimer(uri);
            lastDiagnosticVersion.delete(uri);
            connection.sendDiagnostics({ uri, diagnostics: [] });
        },

        onIndexUpdate(documents: Map<string, TextDocument>): void {
            lastDiagnosticVersion.clear();
            for (const doc of documents.values()) {
                const key = doc.uri;
                clearTimer(key);
                debounceTimers.set(key, setTimeout(async () => {
                    await updateDiagnostics(doc);
                    debounceTimers.delete(key);
                }, 300));
            }
        },
    };
}

function uriToPath(uri: string): string {
    const url = new URL(uri);
    return decodeURIComponent(url.pathname.replace(/^\/([A-Za-z]:)/, '$1'));
}
