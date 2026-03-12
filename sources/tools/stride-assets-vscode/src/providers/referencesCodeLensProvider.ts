import * as vscode from 'vscode';
import { AssetIndex } from '../core/assetIndex';
import { parseAssetHeader } from '../core/assetParser';

// Regex to find indented Id: GUID lines (part definitions in scenes/prefabs/UI pages)
const PART_ID_REGEX = /^(\s+)Id:\s+([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/i;
// The asset's own Id on line 1 (no indentation)
const ASSET_ID_REGEX = /^Id:\s+([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/i;

export class StrideReferencesCodeLensProvider implements vscode.CodeLensProvider {
    private _onDidChangeCodeLenses = new vscode.EventEmitter<void>();
    readonly onDidChangeCodeLenses = this._onDidChangeCodeLenses.event;

    constructor(private index: AssetIndex) {
        // Refresh code lenses when index updates
        index.onDidUpdate(() => this._onDidChangeCodeLenses.fire());
    }

    provideCodeLenses(
        document: vscode.TextDocument,
        _token: vscode.CancellationToken
    ): vscode.CodeLens[] {
        const lenses: vscode.CodeLens[] = [];
        const lines = document.getText().split('\n');

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
                const backRefs = this.index.getBackRefs(guid);
                if (backRefs.length > 0) {
                    const range = new vscode.Range(i, 0, i, line.length);
                    const locations = backRefs.map(r => new vscode.Location(
                        vscode.Uri.file(r.sourceFilePath),
                        new vscode.Position(r.line, r.column)
                    ));
                    lenses.push(new vscode.CodeLens(range, {
                        title: `${backRefs.length} reference${backRefs.length > 1 ? 's' : ''}`,
                        command: 'editor.action.showReferences',
                        arguments: [
                            document.uri,
                            new vscode.Position(i, 0),
                            locations,
                        ],
                    }));
                }
            }
        }

        return lenses;
    }
}
