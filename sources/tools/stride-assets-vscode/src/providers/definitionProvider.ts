import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { AssetIndex } from '../core/assetIndex';
import { getAssetReferenceAtPosition, getPartReferenceAtPosition, getSourcePathAtPosition, getScriptReferenceAtPosition, getPropertyKeyAtPosition, findContainingScriptType } from '../core/referencePattern';

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

        // Check for part reference (ref!! GUID)
        const partRef = getPartReferenceAtPosition(text, line, col);
        if (partRef) {
            const part = this.index.lookupPart(partRef.guid);
            if (part) {
                return new vscode.Location(
                    vscode.Uri.file(part.filePath),
                    new vscode.Position(part.line, 0)
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

            // Check for property key -> C# field/property navigation
            const propKey = getPropertyKeyAtPosition(text, line, col);
            if (propKey) {
                const containingType = findContainingScriptType(text, line);
                if (containingType && this.index.hasProject(containingType.assemblyName)) {
                    const classLocation = await this.resolveClassLocation(containingType.typeName);
                    if (classLocation) {
                        // Try to navigate to the specific member; fall back to the class
                        const memberLocation = await this.resolveMemberLocation(classLocation, propKey.key);
                        return memberLocation ?? classLocation;
                    }
                }
            }
        }

        return undefined;
    }

    // Resolve a fully-qualified type name to its C# class location
    private async resolveClassLocation(typeName: string): Promise<vscode.Location | undefined> {
        const symbols = await vscode.commands.executeCommand<vscode.SymbolInformation[]>(
            'vscode.executeWorkspaceSymbolProvider',
            typeName
        );
        if (symbols && symbols.length > 0) {
            const className = typeName.split('.').pop();
            const match = symbols.find(s =>
                s.kind === vscode.SymbolKind.Class && s.name === className
            );
            if (match) {
                return match.location;
            }
        }
        return undefined;
    }

    // Find a field or property by name within a C# class file.
    // Uses the document symbol provider with retry (C# language server may need time),
    // then falls back to text search.
    private async resolveMemberLocation(classLocation: vscode.Location, memberName: string): Promise<vscode.Location | undefined> {
        const doc = await vscode.workspace.openTextDocument(classLocation.uri);

        // Try document symbol provider with a few retries (language server may still be loading)
        for (let attempt = 0; attempt < 3; attempt++) {
            const docSymbols = await vscode.commands.executeCommand<vscode.DocumentSymbol[]>(
                'vscode.executeDocumentSymbolProvider',
                classLocation.uri
            );
            if (docSymbols && docSymbols.length > 0) {
                const member = this.findMemberSymbol(docSymbols, memberName);
                if (member) {
                    return new vscode.Location(classLocation.uri, member.selectionRange.start);
                }
                break; // Symbols loaded but member not found — no point retrying
            }
            if (attempt < 2) {
                await new Promise(resolve => setTimeout(resolve, 500));
            }
        }

        // Fallback: text search for a declaration line containing the member name
        const text = doc.getText();
        const lines = text.split('\n');
        const memberRegex = new RegExp(`\\b${memberName}\\b`);
        const declarationPrefix = /\b(?:public|private|protected|internal)\b/;
        for (let i = 0; i < lines.length; i++) {
            const line = lines[i];
            if (declarationPrefix.test(line) && memberRegex.test(line)) {
                const col = line.indexOf(memberName);
                return new vscode.Location(classLocation.uri, new vscode.Position(i, col));
            }
        }
        return undefined;
    }

    // Recursively search document symbols for a field or property by name
    private findMemberSymbol(symbols: vscode.DocumentSymbol[], name: string): vscode.DocumentSymbol | undefined {
        for (const sym of symbols) {
            if (sym.name === name && (sym.kind === vscode.SymbolKind.Field || sym.kind === vscode.SymbolKind.Property)) {
                return sym;
            }
            if (sym.children && sym.children.length > 0) {
                const found = this.findMemberSymbol(sym.children, name);
                if (found) { return found; }
            }
        }
        return undefined;
    }
}
