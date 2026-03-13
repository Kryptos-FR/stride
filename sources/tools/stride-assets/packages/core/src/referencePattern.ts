// Regex patterns for detecting asset references in Stride .sd* files

const GUID_PATTERN = '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}';

// Cross-asset reference: GUID:AssetName
// Examples:
//   f16ed18b-ed49-4471-ab10-bb4ee742bdbb:BG_00 Texture
//   920f4445-c00d-474f-b8c0-33b385d58b8f:character_00
//   48c406d2-677a-4a3b-90a0-4c0094894935:VFXPrefabs/vfx-GetCoin
// The name portion extends to end of line (trimmed), but stops at }, ], or comma
const ASSET_REFERENCE_REGEX = new RegExp(
    `(${GUID_PATTERN}):([^\\s,}\\]]+(?:\\s+[^\\s,}\\]:]+)*)`,
    'gi'
);

// Internal part reference: ref!! GUID
// Used within scenes/prefabs/UI pages to reference parts in the same file
const PART_REFERENCE_REGEX = new RegExp(
    `ref!!\\s+(${GUID_PATTERN})`,
    'gi'
);

// Source path: a line starting with Source: followed by a relative path
// No $ anchor — lines may have trailing \r on Windows (\r\n line endings)
const SOURCE_PATH_REGEX = /^(\s*Source:\s+)(.+)/gm;

// FontSource path (for .sdfnt files): FontSource: !SystemFontProvider or !FileFontProvider with FontName/Source
const FONT_SOURCE_REGEX = /^(\s*(?:Source|FontName):\s+)(.+)/gm;

// Script/component type reference: !Namespace.TypeName,AssemblyName
// Examples:
//   !SpaceEscape.GameScript,SpaceEscape.Game
//   !SpaceEscape.Background.BackgroundInfo,SpaceEscape.Game
//   !Stride.Rendering.MeshRenderFeature,Stride.Rendering
// The type name must have at least one dot (single-word !tags are YAML type markers, not scripts)
const SCRIPT_REFERENCE_REGEX = /!([\w]+(?:\.[\w]+)+)\s*,\s*([\w]+(?:\.[\w]+)*)/;

// Standalone GUID pattern for validation
const GUID_REGEX = new RegExp(`^${GUID_PATTERN}$`, 'i');

export interface AssetReferenceMatch {
    guid: string;
    name: string;
    fullMatch: string;
    index: number;
    line: number;
    startColumn: number;
    endColumn: number;
}

export interface PartReferenceMatch {
    guid: string;
    fullMatch: string;
    index: number;
    line: number;
    startColumn: number;
    endColumn: number;
}

export interface SourcePathMatch {
    path: string;
    fullMatch: string;
    index: number;
    line: number;
    startColumn: number;
    endColumn: number;
}

export interface ScriptReferenceMatch {
    typeName: string;       // e.g., "SpaceEscape.Background.BackgroundInfo"
    assemblyName: string;   // e.g., "SpaceEscape.Game"
    fullMatch: string;
    line: number;
    startColumn: number;
    endColumn: number;
}

export function findAssetReferences(text: string): AssetReferenceMatch[] {
    const results: AssetReferenceMatch[] = [];
    const lines = text.split('\n');

    for (let lineNum = 0; lineNum < lines.length; lineNum++) {
        const line = lines[lineNum];
        const regex = new RegExp(ASSET_REFERENCE_REGEX.source, 'gi');
        let match: RegExpExecArray | null;
        while ((match = regex.exec(line)) !== null) {
            const name = match[2].trimEnd();
            results.push({
                guid: match[1].toLowerCase(),
                name,
                fullMatch: match[1] + ':' + name,
                index: match.index,
                line: lineNum,
                startColumn: match.index,
                endColumn: match.index + match[1].length + 1 + name.length,
            });
        }
    }

    return results;
}

export function findPartReferences(text: string): PartReferenceMatch[] {
    const results: PartReferenceMatch[] = [];
    const lines = text.split('\n');

    for (let lineNum = 0; lineNum < lines.length; lineNum++) {
        const line = lines[lineNum];
        const regex = new RegExp(PART_REFERENCE_REGEX.source, 'gi');
        let match: RegExpExecArray | null;
        while ((match = regex.exec(line)) !== null) {
            results.push({
                guid: match[1].toLowerCase(),
                fullMatch: match[0],
                index: match.index,
                line: lineNum,
                startColumn: match.index,
                endColumn: match.index + match[0].length,
            });
        }
    }

    return results;
}

export function findSourcePaths(text: string): SourcePathMatch[] {
    const results: SourcePathMatch[] = [];
    const lines = text.split('\n');

    for (let lineNum = 0; lineNum < lines.length; lineNum++) {
        const line = lines[lineNum];
        const regex = new RegExp(SOURCE_PATH_REGEX.source, 'g');
        const match = regex.exec(line);
        if (match) {
            const prefix = match[1];
            const path = match[2].trimEnd();
            // Skip YAML type tags like !SystemFontProvider, !dir, etc.
            if (path.startsWith('!') || path === 'null') {
                continue;
            }
            results.push({
                path,
                fullMatch: match[0],
                index: match.index,
                line: lineNum,
                startColumn: prefix.length,
                endColumn: prefix.length + path.length,
            });
        }
    }

    return results;
}

export function findScriptReferences(text: string): ScriptReferenceMatch[] {
    const results: ScriptReferenceMatch[] = [];
    const lines = text.split('\n');

    for (let lineNum = 0; lineNum < lines.length; lineNum++) {
        const line = lines[lineNum];
        const match = SCRIPT_REFERENCE_REGEX.exec(line);
        if (match) {
            const typeName = match[1];
            const assemblyName = match[2].trimEnd();
            const startCol = match.index;
            // Full match includes the ! prefix
            const fullMatch = match[0].trimEnd();
            results.push({
                typeName,
                assemblyName,
                fullMatch,
                line: lineNum,
                startColumn: startCol,
                endColumn: startCol + fullMatch.length,
            });
        }
    }

    return results;
}

export function getScriptReferenceAtPosition(text: string, line: number, column: number): ScriptReferenceMatch | undefined {
    const refs = findScriptReferences(text);
    return refs.find(r =>
        r.line === line && column >= r.startColumn && column <= r.endColumn
    );
}

export function isGuid(text: string): boolean {
    return GUID_REGEX.test(text);
}

// Given a position (line, column) in text, find the asset reference at that position
export function getAssetReferenceAtPosition(text: string, line: number, column: number): AssetReferenceMatch | undefined {
    const refs = findAssetReferences(text);
    return refs.find(r =>
        r.line === line && column >= r.startColumn && column <= r.endColumn
    );
}

// Given a position (line, column) in text, find the part reference at that position
export function getPartReferenceAtPosition(text: string, line: number, column: number): PartReferenceMatch | undefined {
    const refs = findPartReferences(text);
    return refs.find(r =>
        r.line === line && column >= r.startColumn && column <= r.endColumn
    );
}

// Given a position (line, column) in text, find the source path at that position
export function getSourcePathAtPosition(text: string, line: number, column: number): SourcePathMatch | undefined {
    const refs = findSourcePaths(text);
    return refs.find(r =>
        r.line === line && column >= r.startColumn && column <= r.endColumn
    );
}

// --- Property key navigation (YAML key -> C# field/property) ---

export interface PropertyKeyMatch {
    key: string;          // e.g., "CharacterShadow"
    line: number;
    startColumn: number;
    endColumn: number;
}

// YAML property key: indented word followed by optional * (archetype override) and colon
const PROPERTY_KEY_REGEX = /^(\s+)(\w+)\*?:\s/;

// Structural keys that are part of the Stride asset schema, not C# members
const STRUCTURAL_KEYS = new Set([
    'Id', 'Name', 'Components', 'Children', 'Entity', 'UIElement',
    'Parts', 'RootParts', 'Hierarchy', 'BasePartAsset', 'BasePartId',
    'Archetype', 'Base', 'Design', 'Tags', 'Group',
]);

// Given a position, check if the cursor is on a YAML property key
export function getPropertyKeyAtPosition(text: string, line: number, column: number): PropertyKeyMatch | undefined {
    const lines = text.split('\n');
    if (line >= lines.length) { return undefined; }

    const lineText = lines[line];
    const match = PROPERTY_KEY_REGEX.exec(lineText);
    if (!match) { return undefined; }

    const indent = match[1].length;
    const key = match[2];
    const startColumn = indent;
    const endColumn = indent + key.length;

    // Cursor must be within the key span
    if (column < startColumn || column > endColumn) { return undefined; }

    // Skip structural keys
    if (STRUCTURAL_KEYS.has(key)) { return undefined; }

    return { key, line, startColumn, endColumn };
}

// Walk backwards from a property line to find the containing !Type,Assembly component block
export function findContainingScriptType(text: string, lineNum: number): ScriptReferenceMatch | undefined {
    const lines = text.split('\n');
    if (lineNum >= lines.length) { return undefined; }

    const propertyLine = lines[lineNum];
    const propertyIndent = propertyLine.length - propertyLine.trimStart().length;

    for (let i = lineNum - 1; i >= 0; i--) {
        const line = lines[i];
        if (line.trim().length === 0) { continue; }

        const lineIndent = line.length - line.trimStart().length;
        if (lineIndent < propertyIndent) {
            // Check if this line contains any !TypeTag (component/type declaration)
            if (/!\w+/.test(line)) {
                // It's a type declaration — check if it's a user script (!Namespace.Type,Assembly)
                const scriptMatch = SCRIPT_REFERENCE_REGEX.exec(line);
                if (scriptMatch) {
                    const typeName = scriptMatch[1];
                    const assemblyName = scriptMatch[2].trimEnd();
                    const fullMatch = scriptMatch[0].trimEnd();
                    return {
                        typeName,
                        assemblyName,
                        fullMatch,
                        line: i,
                        startColumn: scriptMatch.index,
                        endColumn: scriptMatch.index + fullMatch.length,
                    };
                }
                // Framework type (e.g., !TransformComponent) — stop searching
                return undefined;
            }
            // Not a type line (e.g., "Components:", "Children:") — keep walking back
        }
    }

    return undefined;
}
