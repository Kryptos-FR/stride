import { RequestType } from 'vscode-languageserver';

// Custom LSP request: server asks client to resolve a C# symbol location.
// Used for script navigation (Ctrl+click on !Type,Assembly) and
// property key navigation (Ctrl+click on YAML keys → C# fields).
export interface ResolveCSharpSymbolParams {
    typeName: string;       // e.g., "SpaceEscape.CharacterScript"
    memberName?: string;    // e.g., "CharacterShadow" (for property key navigation)
}

export interface ResolveCSharpSymbolResult {
    location?: {
        uri: string;
        range: {
            start: { line: number; character: number };
            end: { line: number; character: number };
        };
    };
}

export const ResolveCSharpSymbolRequest = new RequestType<
    ResolveCSharpSymbolParams,
    ResolveCSharpSymbolResult,
    void
>('stride/resolveCSharpSymbol');
