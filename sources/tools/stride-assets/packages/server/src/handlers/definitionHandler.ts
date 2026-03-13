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
    return {
        async handle(params: DefinitionParams, doc: TextDocument | undefined): Promise<Location | Location[] | null> {
            if (!doc) { return null; }

            const text = doc.getText();
            const line = params.position.line;
            const col = params.position.character;

            // Check for cross-asset reference (GUID:Name)
            const assetRef = getAssetReferenceAtPosition(text, line, col);
            if (assetRef) {
                const entry = index.lookupGuid(assetRef.guid);
                if (entry) {
                    return Location.create(pathToUri(entry.filePath), { start: Position.create(0, 0), end: Position.create(0, 0) });
                }
                return null;
            }

            // Check for part reference (ref!! GUID)
            const partRef = getPartReferenceAtPosition(text, line, col);
            if (partRef) {
                const part = index.lookupPart(partRef.guid);
                if (part) {
                    return Location.create(pathToUri(part.filePath), { start: Position.create(part.line, 0), end: Position.create(part.line, 0) });
                }
                return null;
            }

            // Check for source path
            const sourcePath = getSourcePathAtPosition(text, line, col);
            if (sourcePath) {
                const dir = path.dirname(uriToPath(doc.uri));
                const resolved = path.resolve(dir, sourcePath.path);
                try {
                    await fs.promises.access(resolved);
                    return Location.create(pathToUri(resolved), { start: Position.create(0, 0), end: Position.create(0, 0) });
                } catch {
                    return null;
                }
            }

            // Script and property key navigation (opt-in)
            const settings = getSettings();
            if (settings.scriptNavigationEnabled) {
                // Check for script reference (!Type,Assembly)
                const scriptRef = getScriptReferenceAtPosition(text, line, col);
                if (scriptRef) {
                    const result = await connection.sendRequest(ResolveCSharpSymbolRequest, {
                        typeName: scriptRef.typeName,
                    });
                    if (result.location) {
                        return Location.create(result.location.uri, result.location.range);
                    }
                    return null;
                }

                // Check for property key -> C# field/property
                const propKey = getPropertyKeyAtPosition(text, line, col);
                if (propKey) {
                    const containingType = findContainingScriptType(text, line);
                    if (containingType && index.hasProject(containingType.assemblyName)) {
                        const result = await connection.sendRequest(ResolveCSharpSymbolRequest, {
                            typeName: containingType.typeName,
                            memberName: propKey.key,
                        });
                        if (result.location) {
                            return Location.create(result.location.uri, result.location.range);
                        }
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
    return decodeURIComponent(url.pathname.replace(/^\/([A-Za-z]:)/, '$1'));
}
