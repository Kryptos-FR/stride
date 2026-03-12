import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { AssetIndex } from '../core/assetIndex';
import { getAssetReferenceAtPosition, getEntityReferenceAtPosition, getSourcePathAtPosition, getScriptReferenceAtPosition } from '../core/referencePattern';

export class StrideDefinitionProvider implements vscode.DefinitionProvider {
    constructor(private index: AssetIndex) {}

    async provideDefinition(
        document: vscode.TextDocument,
        position: vscode.Position,
        _token: vscode.CancellationToken
    ): Promise<vscode.Definition | undefined> {
        const text = document.getText();
        const line = position.line;
        const col = position.character;

        // Check for cross-asset reference (GUID:Name)
        const assetRef = getAssetReferenceAtPosition(text, line, col);
        if (assetRef) {
            const entry = this.index.lookupGuid(assetRef.guid);
            if (entry) {
                // Reveal in Explorer on Ctrl+hover; Ctrl+click will also open the file
                await vscode.commands.executeCommand('revealInExplorer', vscode.Uri.file(entry.filePath));
                return new vscode.Location(
                    vscode.Uri.file(entry.filePath),
                    new vscode.Position(0, 0)
                );
            }
            return undefined;
        }

        // Check for entity reference (ref!! GUID)
        const entityRef = getEntityReferenceAtPosition(text, line, col);
        if (entityRef) {
            const entity = this.index.lookupEntity(entityRef.guid);
            if (entity) {
                return new vscode.Location(
                    vscode.Uri.file(entity.filePath),
                    new vscode.Position(entity.line, 0)
                );
            }
            return undefined;
        }

        // Check for source path
        const sourcePath = getSourcePathAtPosition(text, line, col);
        if (sourcePath) {
            const dir = path.dirname(document.uri.fsPath);
            const resolved = path.resolve(dir, sourcePath.path);
            if (fs.existsSync(resolved)) {
                // Reveal in Explorer on Ctrl+hover; Ctrl+click will also open the file
                await vscode.commands.executeCommand('revealInExplorer', vscode.Uri.file(resolved));
                return new vscode.Location(
                    vscode.Uri.file(resolved),
                    new vscode.Position(0, 0)
                );
            } else {
                vscode.window.showErrorMessage(`Source file not found: ${sourcePath.path}`);
            }
            return undefined;
        }

        // Check for script/component type reference (!TypeName,Assembly)
        const scriptNavigationEnabled = vscode.workspace.getConfiguration('strideAssets').get<boolean>('scriptNavigationEnabled', false);
        if (scriptNavigationEnabled) {
            const scriptRef = getScriptReferenceAtPosition(text, line, col);
            if (scriptRef) {
                const symbols = await vscode.commands.executeCommand<vscode.SymbolInformation[]>(
                    'vscode.executeWorkspaceSymbolProvider',
                    scriptRef.typeName
                );
                if (symbols && symbols.length > 0) {
                    const className = scriptRef.typeName.split('.').pop();
                    const match = symbols.find(s =>
                        s.kind === vscode.SymbolKind.Class && s.name === className
                    );
                    if (match) {
                        return match.location;
                    }
                }
                return undefined;
            }
        }

        return undefined;
    }
}
