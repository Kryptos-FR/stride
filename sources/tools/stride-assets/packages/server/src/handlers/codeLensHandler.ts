import { CodeLensParams, CodeLens, Range, Position, Command } from 'vscode-languageserver';
import { TextDocument } from 'vscode-languageserver-textdocument';
import { AssetIndex } from '../assetIndex';
import { parseAssetHeader } from 'stride-assets-core';

// Regex to find indented Id: GUID lines (part definitions in scenes/prefabs/UI pages)
const PART_ID_REGEX = /^(\s+)Id:\s+([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/i;
// The asset's own Id on line 1 (no indentation)
const ASSET_ID_REGEX = /^Id:\s+([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/i;

export function createCodeLensHandler(index: AssetIndex) {
    return {
        handle(params: CodeLensParams, doc: TextDocument | undefined): CodeLens[] {
            if (!doc) { return []; }

            const lenses: CodeLens[] = [];
            const lines = doc.getText().split('\n');

            for (let i = 0; i < lines.length; i++) {
                const line = lines[i];
                let guid: string | undefined;

                // Check for asset's own Id (line 1, no indentation)
                if (i <= 1) {
                    const assetMatch = ASSET_ID_REGEX.exec(line);
                    if (assetMatch) {
                        guid = assetMatch[1].toLowerCase();
                    }
                }

                // Check for part Id (indented)
                if (!guid) {
                    const partMatch = PART_ID_REGEX.exec(line);
                    if (partMatch && partMatch[1].length > 0) {
                        guid = partMatch[2].toLowerCase();
                    }
                }

                if (guid) {
                    const backRefs = index.getBackRefs(guid);
                    if (backRefs.length > 0) {
                        const range = Range.create(Position.create(i, 0), Position.create(i, line.length));
                        // Use editor.action.showReferences — works in VS Code.
                        // Other clients may need to map this command.
                        const locations = backRefs.map(r => ({
                            uri: pathToUri(r.sourceFilePath),
                            range: Range.create(Position.create(r.line, r.column), Position.create(r.line, r.column)),
                        }));
                        lenses.push({
                            range,
                            command: Command.create(
                                `${backRefs.length} reference${backRefs.length > 1 ? 's' : ''}`,
                                'editor.action.showReferences',
                                doc.uri,
                                Position.create(i, 0),
                                locations,
                            ),
                        });
                    }
                }
            }

            return lenses;
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
