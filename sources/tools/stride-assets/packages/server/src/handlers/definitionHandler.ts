import * as fs from 'fs';
import * as path from 'path';
import { Connection, DefinitionParams, Location, Position } from 'vscode-languageserver';
import { TextDocument } from 'vscode-languageserver-textdocument';
import { AssetIndex } from '../assetIndex';
import {
    getAssetReferenceAtPosition,
    getPartReferenceAtPosition,
    getSourcePathAtPosition,
    getScriptReferenceAtPosition,
    getPropertyKeyAtPosition,
    findContainingScriptType,
} from 'stride-assets-core';
import { ResolveCSharpSymbolRequest } from '../protocol';

interface Settings {
    scriptNavigationEnabled: boolean;
}

export function createDefinitionHandler(
    index: AssetIndex,
    connection: Connection,
    getSettings: () => Settings,
) {
    function log(msg: string): void {
        connection.console.log(`[Stride:Definition] ${msg}`);
    }

    return {
        async handle(params: DefinitionParams, doc: TextDocument | undefined): Promise<Location | Location[] | null> {
            if (!doc) { return null; }

            const text = doc.getText();
            const line = params.position.line;
            const col = params.position.character;
            const textLines = text.split('\n');
            const lineText = textLines[line] ?? '';
            log(`Request at line ${line}, col ${col}: "${lineText.trim().substring(0, 80)}"`);

            // Check for cross-asset reference (GUID:Name)
            const assetRef = getAssetReferenceAtPosition(text, line, col);
            if (assetRef) {
                log(`Asset reference: guid=${assetRef.guid}, name=${assetRef.name}`);
                const entry = index.lookupGuid(assetRef.guid);
                if (entry) {
                    log(`  -> found: ${entry.filePath}`);
                    return Location.create(pathToUri(entry.filePath), { start: Position.create(0, 0), end: Position.create(0, 0) });
                }
                log(`  -> not found in index (${index.size} assets)`);
                return null;
            }

            // Check for part reference (ref!! GUID)
            const partRef = getPartReferenceAtPosition(text, line, col);
            if (partRef) {
                log(`Part reference: guid=${partRef.guid}`);
                const part = index.lookupPart(partRef.guid);
                if (part) {
                    log(`  -> found: ${part.filePath}:${part.line} (${part.name})`);
                    return Location.create(pathToUri(part.filePath), { start: Position.create(part.line, 0), end: Position.create(part.line, 0) });
                }
                log(`  -> not found in part index`);
                return null;
            }

            // Check for source path
            const sourcePath = getSourcePathAtPosition(text, line, col);
            if (sourcePath) {
                const dir = path.dirname(uriToPath(doc.uri));
                const resolved = path.resolve(dir, sourcePath.path);
                log(`Source path: ${sourcePath.path} -> ${resolved}`);
                try {
                    await fs.promises.access(resolved);
                    log(`  -> file exists`);
                    return Location.create(pathToUri(resolved), { start: Position.create(0, 0), end: Position.create(0, 0) });
                } catch {
                    log(`  -> file not found`);
                    return null;
                }
            }

            // Script and property key navigation (opt-in)
            const settings = getSettings();
            if (settings.scriptNavigationEnabled) {
                // Check for script reference (!Type,Assembly)
                const scriptRef = getScriptReferenceAtPosition(text, line, col);
                if (scriptRef) {
                    log(`Script reference: type=${scriptRef.typeName}, assembly=${scriptRef.assemblyName}`);
                    try {
                        const result = await connection.sendRequest(ResolveCSharpSymbolRequest, {
                            typeName: scriptRef.typeName,
                        });
                        if (result.location) {
                            log(`  -> resolved: ${result.location.uri}:${result.location.range.start.line}`);
                            return Location.create(result.location.uri, result.location.range);
                        }
                        log(`  -> not resolved by client`);
                    } catch (err) {
                        log(`  -> error: ${err}`);
                    }
                    return null;
                }

                // Check for property key -> C# field/property
                const propKey = getPropertyKeyAtPosition(text, line, col);
                if (propKey) {
                    log(`Property key: "${propKey.key}"`);
                    const containingType = findContainingScriptType(text, line);
                    if (containingType) {
                        log(`  containing type: ${containingType.typeName} (${containingType.assemblyName})`);
                        if (index.hasProject(containingType.assemblyName)) {
                            try {
                                const result = await connection.sendRequest(ResolveCSharpSymbolRequest, {
                                    typeName: containingType.typeName,
                                    memberName: propKey.key,
                                });
                                if (result.location) {
                                    log(`  -> resolved member: ${result.location.uri}:${result.location.range.start.line}`);
                                    return Location.create(result.location.uri, result.location.range);
                                }
                                log(`  -> member not resolved by client`);
                            } catch (err) {
                                log(`  -> error: ${err}`);
                            }
                        } else {
                            log(`  -> assembly "${containingType.assemblyName}" not in local projects`);
                        }
                    } else {
                        log(`  -> no containing script type found`);
                    }
                }
            }

            return null;
        },
    };
}

function pathToUri(filePath: string): string {
    const normalized = filePath.replace(/\\/g, '/');
    if (/^[A-Za-z]:/.test(normalized)) {
        return `file:///${normalized}`;
    }
    return `file://${normalized}`;
}

function uriToPath(uri: string): string {
    const url = new URL(uri);
    return decodeURIComponent(url.pathname).replace(/^\/([A-Za-z]:)/, '$1');
}
