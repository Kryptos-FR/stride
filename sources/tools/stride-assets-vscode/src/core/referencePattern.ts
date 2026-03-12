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

// Internal entity reference: ref!! GUID
// Used within scenes/prefabs to reference entities in the same file
const ENTITY_REFERENCE_REGEX = new RegExp(
    `ref!!\\s+(${GUID_PATTERN})`,
    'gi'
);

// Source path: a line starting with Source: followed by a relative path
// No $ anchor — lines may have trailing \r on Windows (\r\n line endings)
const SOURCE_PATH_REGEX = /^(\s*Source:\s+)(.+)/gm;

// FontSource path (for .sdfnt files): FontSource: !SystemFontProvider or !FileFontProvider with FontName/Source
const FONT_SOURCE_REGEX = /^(\s*(?:Source|FontName):\s+)(.+)/gm;

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

export interface EntityReferenceMatch {
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

export function findEntityReferences(text: string): EntityReferenceMatch[] {
    const results: EntityReferenceMatch[] = [];
    const lines = text.split('\n');

    for (let lineNum = 0; lineNum < lines.length; lineNum++) {
        const line = lines[lineNum];
        const regex = new RegExp(ENTITY_REFERENCE_REGEX.source, 'gi');
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

// Given a position (line, column) in text, find the entity reference at that position
export function getEntityReferenceAtPosition(text: string, line: number, column: number): EntityReferenceMatch | undefined {
    const refs = findEntityReferences(text);
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
