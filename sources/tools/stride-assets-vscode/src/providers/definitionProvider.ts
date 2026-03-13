import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { AssetIndex } from '../core/assetIndex';
import { getAssetReferenceAtPosition, getPartReferenceAtPosition, getSourcePathAtPosition, getScriptReferenceAtPosition, getPropertyKeyAtPosition, findContainingScriptType } from '../core/referencePattern';
import { log } from '../core/logger';

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

        log(`provideDefinition at ${vscode.workspace.asRelativePath(document.uri)}:${line + 1}:${col}`);

        // Check for cross-asset reference (GUID:Name)
        const assetRef = getAssetReferenceAtPosition(text, line, col);
        if (assetRef) {
            const entry = this.index.lookupGuid(assetRef.guid);
            log(`  asset ref: ${assetRef.guid}:${assetRef.name} → ${entry ? vscode.workspace.asRelativePath(entry.filePath) : 'NOT FOUND'}`);
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
            log(`  part ref: ${partRef.guid} → ${part ? `${vscode.workspace.asRelativePath(part.filePath)}:${part.line + 1} (${part.name ?? 'unnamed'})` : 'NOT FOUND'}`);
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
            const exists = fs.existsSync(resolved);
            log(`  source path: ${sourcePath.path} → ${exists ? resolved : 'NOT FOUND'}`);
            if (exists) {
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
                log(`  script ref: !${scriptRef.typeName},${scriptRef.assemblyName}`);
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
                        log(`    → class found: ${vscode.workspace.asRelativePath(match.location.uri)}`);
                        return match.location;
                    }
                }
                log(`    → class NOT FOUND (${symbols?.length ?? 0} symbols returned)`);
                return undefined;
            }

            // Check for property key -> C# field/property navigation
            const propKey = getPropertyKeyAtPosition(text, line, col);
            if (propKey) {
                const containingType = findContainingScriptType(text, line);
                log(`  property key: "${propKey.key}" → containing type: ${containingType ? `!${containingType.typeName},${containingType.assemblyName} (line ${containingType.line + 1})` : 'NONE'}`);
                if (containingType) {
                    const isLocal = this.index.hasProject(containingType.assemblyName);
                    log(`    assembly "${containingType.assemblyName}" is local: ${isLocal}`);
                    if (isLocal) {
                        const classLocation = await this.resolveClassLocation(containingType.typeName);
                        log(`    class location: ${classLocation ? vscode.workspace.asRelativePath(classLocation.uri) : 'NOT FOUND'}`);
                        if (classLocation) {
                            // Try to navigate to the specific member; fall back to the class
                            const memberLocation = await this.resolveMemberLocation(classLocation, propKey.key);
                            log(`    member "${propKey.key}": ${memberLocation ? `found at ${memberLocation.range.start.line + 1}:${memberLocation.range.start.character}` : 'NOT FOUND, falling back to class'}`);
                            return memberLocation ?? classLocation;
                        }
                    }
                }
            } else {
                log(`  no match at position (not a reference or property key)`);
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
            log(`    docSymbolProvider attempt ${attempt + 1}: ${docSymbols ? `${docSymbols.length} symbols` : 'undefined'}`);
            if (docSymbols && docSymbols.length > 0) {
                this.logSymbolTree(docSymbols, '      ');
                const member = this.findMemberSymbol(docSymbols, memberName);
                if (member) {
                    log(`    docSymbolProvider: found "${memberName}" (kind=${member.kind}) at line ${member.selectionRange.start.line + 1}`);
                    return new vscode.Location(classLocation.uri, member.selectionRange.start);
                }
                log(`    docSymbolProvider: "${memberName}" not found among symbols`);
                break; // Symbols loaded but member not found — no point retrying
            }
            if (attempt < 2) {
                await new Promise(resolve => setTimeout(resolve, 500));
            }
        }

        // Fallback: text search for a declaration line containing the member name
        log(`    falling back to text search for "${memberName}"`);
        const text = doc.getText();
        const lines = text.split('\n');
        const memberRegex = new RegExp(`\\b${memberName}\\b`);
        const declarationPrefix = /\b(?:public|private|protected|internal)\b/;
        for (let i = 0; i < lines.length; i++) {
            const line = lines[i];
            if (declarationPrefix.test(line) && memberRegex.test(line)) {
                const col = line.indexOf(memberName);
                log(`    text search: found "${memberName}" at line ${i + 1}`);
                return new vscode.Location(classLocation.uri, new vscode.Position(i, col));
            }
        }
        log(`    text search: "${memberName}" not found`);
        return undefined;
    }

    // Log the symbol tree for debugging
    private logSymbolTree(symbols: vscode.DocumentSymbol[], indent: string): void {
        for (const sym of symbols) {
            log(`${indent}${sym.name} (kind=${sym.kind}, children=${sym.children?.length ?? 0})`);
            if (sym.children && sym.children.length > 0) {
                this.logSymbolTree(sym.children, indent + '  ');
            }
        }
    }

    // Recursively search document symbols for a field or property by name.
    // C# extension may return names like "Font : SpriteFont" — extract the member name before comparing.
    private findMemberSymbol(symbols: vscode.DocumentSymbol[], name: string): vscode.DocumentSymbol | undefined {
        for (const sym of symbols) {
            const symName = sym.name.split(' : ')[0];
            if (symName === name && (sym.kind === vscode.SymbolKind.Field || sym.kind === vscode.SymbolKind.Property)) {
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
