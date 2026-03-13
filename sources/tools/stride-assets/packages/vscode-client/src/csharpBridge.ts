import * as vscode from 'vscode';
import { LanguageClient, RequestType } from 'vscode-languageclient/node';

// Must match the server's protocol.ts definitions
interface ResolveCSharpSymbolParams {
    typeName: string;
    memberName?: string;
}

interface ResolveCSharpSymbolResult {
    location?: {
        uri: string;
        range: {
            start: { line: number; character: number };
            end: { line: number; character: number };
        };
    };
}

const ResolveCSharpSymbolRequest = new RequestType<
    ResolveCSharpSymbolParams,
    ResolveCSharpSymbolResult,
    void
>('stride/resolveCSharpSymbol');

let outputChannel: vscode.OutputChannel;

function log(msg: string): void {
    outputChannel?.appendLine(`[CSharpBridge] ${msg}`);
}

// Register the handler for the custom request from the server.
// When the server needs to resolve a C# symbol, it sends this request
// and we use VS Code's C# extension to fulfill it.
export function registerCSharpBridge(client: LanguageClient): void {
    outputChannel = vscode.window.createOutputChannel('Stride C# Bridge');

    client.onRequest(ResolveCSharpSymbolRequest, async (params) => {
        log(`Request: typeName="${params.typeName}", memberName="${params.memberName ?? '(none)'}"`);
        const result = await resolveCSharpSymbol(params);
        if (result.location) {
            log(`  -> resolved: ${result.location.uri}:${result.location.range.start.line}`);
        } else {
            log(`  -> not resolved`);
        }
        return result;
    });

    log('C# bridge registered');
}

async function resolveCSharpSymbol(params: ResolveCSharpSymbolParams): Promise<ResolveCSharpSymbolResult> {
    // Find the class using workspace symbol provider
    const classLocation = await resolveClassLocation(params.typeName);
    if (!classLocation) {
        log(`  class not found: ${params.typeName}`);
        return {};
    }

    log(`  class found: ${classLocation.uri.fsPath}:${classLocation.range.start.line}`);

    // If no member requested, return the class location
    if (!params.memberName) {
        return {
            location: {
                uri: classLocation.uri.toString(),
                range: {
                    start: { line: classLocation.range.start.line, character: classLocation.range.start.character },
                    end: { line: classLocation.range.end.line, character: classLocation.range.end.character },
                },
            },
        };
    }

    // Try to find the specific member
    log(`  looking for member "${params.memberName}" in ${classLocation.uri.fsPath}`);
    const memberLocation = await resolveMemberLocation(classLocation, params.memberName);
    if (memberLocation) {
        log(`  member found at line ${memberLocation.range.start.line}`);
    } else {
        log(`  member "${params.memberName}" not found, falling back to class location`);
    }
    const loc = memberLocation ?? classLocation;
    return {
        location: {
            uri: loc.uri.toString(),
            range: {
                start: { line: loc.range.start.line, character: loc.range.start.character },
                end: { line: loc.range.end.line, character: loc.range.end.character },
            },
        },
    };
}

async function resolveClassLocation(typeName: string): Promise<vscode.Location | undefined> {
    log(`  resolveClassLocation: querying workspace symbols for "${typeName}"`);
    const symbols = await vscode.commands.executeCommand<vscode.SymbolInformation[]>(
        'vscode.executeWorkspaceSymbolProvider',
        typeName
    );
    if (symbols && symbols.length > 0) {
        const className = typeName.split('.').pop();
        log(`  found ${symbols.length} workspace symbol(s), looking for class "${className}"`);
        for (const s of symbols.slice(0, 10)) {
            log(`    symbol: name="${s.name}", kind=${vscode.SymbolKind[s.kind]}, container="${s.containerName}"`);
        }
        const match = symbols.find(s =>
            s.kind === vscode.SymbolKind.Class && s.name === className
        );
        if (match) {
            log(`  matched class: ${match.name} in ${match.location.uri.fsPath}`);
            return match.location;
        }
        log(`  no matching class found among ${symbols.length} symbols`);
    } else {
        log(`  no workspace symbols found for "${typeName}"`);
    }
    return undefined;
}

async function resolveMemberLocation(classLocation: vscode.Location, memberName: string): Promise<vscode.Location | undefined> {
    // Try document symbol provider
    log(`  resolveMemberLocation: querying document symbols for ${classLocation.uri.fsPath}`);
    const doc = await vscode.workspace.openTextDocument(classLocation.uri);
    const docSymbols = await vscode.commands.executeCommand<vscode.DocumentSymbol[]>(
        'vscode.executeDocumentSymbolProvider',
        classLocation.uri
    );

    if (docSymbols && docSymbols.length > 0) {
        log(`  found ${docSymbols.length} top-level document symbol(s)`);
        for (const sym of docSymbols) {
            log(`    symbol: name="${sym.name}", kind=${vscode.SymbolKind[sym.kind]}, children=${sym.children?.length ?? 0}`);
            if (sym.children) {
                for (const child of sym.children.slice(0, 20)) {
                    log(`      child: name="${child.name}", kind=${vscode.SymbolKind[child.kind]}`);
                }
            }
        }
        const member = findMemberSymbol(docSymbols, memberName);
        if (member) {
            log(`  found member "${member.name}" (${vscode.SymbolKind[member.kind]}) at line ${member.selectionRange.start.line}`);
            return new vscode.Location(classLocation.uri, member.selectionRange.start);
        }
        log(`  member "${memberName}" not found via document symbols`);
    } else {
        log(`  no document symbols returned`);
    }

    // Fallback: text search for a declaration line containing the member name
    log(`  trying text search fallback for "${memberName}"`);
    const text = doc.getText();
    const lines = text.split('\n');
    const memberRegex = new RegExp(`\\b${memberName}\\b`);
    const declarationPrefix = /\b(?:public|private|protected|internal)\b/;
    for (let i = 0; i < lines.length; i++) {
        const line = lines[i];
        if (declarationPrefix.test(line) && memberRegex.test(line)) {
            const col = line.indexOf(memberName);
            log(`  text search match at line ${i}: "${line.trim().substring(0, 80)}"`);
            return new vscode.Location(classLocation.uri, new vscode.Position(i, col));
        }
    }

    log(`  text search fallback also failed for "${memberName}"`);
    return undefined;
}

// C# extension returns symbol names like "Font : SpriteFont" — extract just the name
function findMemberSymbol(symbols: vscode.DocumentSymbol[], name: string): vscode.DocumentSymbol | undefined {
    for (const sym of symbols) {
        const symName = sym.name.split(' : ')[0];
        if (symName === name && (sym.kind === vscode.SymbolKind.Field || sym.kind === vscode.SymbolKind.Property)) {
            return sym;
        }
        if (sym.children && sym.children.length > 0) {
            const found = findMemberSymbol(sym.children, name);
            if (found) { return found; }
        }
    }
    return undefined;
}
