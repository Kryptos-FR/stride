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

// Register the handler for the custom request from the server.
// When the server needs to resolve a C# symbol, it sends this request
// and we use VS Code's C# extension to fulfill it.
export function registerCSharpBridge(client: LanguageClient): void {
    client.onRequest(ResolveCSharpSymbolRequest, async (params) => {
        return resolveCSharpSymbol(params);
    });
}

async function resolveCSharpSymbol(params: ResolveCSharpSymbolParams): Promise<ResolveCSharpSymbolResult> {
    // Find the class using workspace symbol provider
    const classLocation = await resolveClassLocation(params.typeName);
    if (!classLocation) {
        return {};
    }

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
    const memberLocation = await resolveMemberLocation(classLocation, params.memberName);
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

async function resolveMemberLocation(classLocation: vscode.Location, memberName: string): Promise<vscode.Location | undefined> {
    // Try document symbol provider
    const doc = await vscode.workspace.openTextDocument(classLocation.uri);
    const docSymbols = await vscode.commands.executeCommand<vscode.DocumentSymbol[]>(
        'vscode.executeDocumentSymbolProvider',
        classLocation.uri
    );

    if (docSymbols && docSymbols.length > 0) {
        const member = findMemberSymbol(docSymbols, memberName);
        if (member) {
            return new vscode.Location(classLocation.uri, member.selectionRange.start);
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
