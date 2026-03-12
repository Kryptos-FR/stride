import * as path from 'path';
import { AssetEntry } from './assetIndex';
import { findAssetReferences, findSourcePaths, AssetReferenceMatch, SourcePathMatch } from './referencePattern';

// Asset type tag (line 1): !TypeName
const TYPE_TAG_REGEX = /^!(\w+)/;
// Asset Id (line 2): Id: GUID
const ID_REGEX = /^Id:\s+([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/i;
// Source path
const SOURCE_REGEX = /^Source:\s+(.+)/;

export interface AssetHeader {
    id: string;
    typeName: string;
    sourcePath?: string;
}

export interface ParsedAsset {
    id: string;
    typeName: string;
    sourcePath?: string;
    references: AssetReferenceMatch[];
    sourcePaths: SourcePathMatch[];
    parts: AssetPartDefinition[];
}

export interface AssetPartDefinition {
    id: string;
    name?: string;
    line: number;
}

// Fast header-only parse: reads just the first few lines to get Id and type
export function parseAssetHeader(content: string): AssetHeader | null {
    const lines = content.split('\n', 10); // Only need first few lines
    if (lines.length < 2) {
        return null;
    }

    const typeMatch = TYPE_TAG_REGEX.exec(lines[0]);
    if (!typeMatch) {
        return null;
    }

    const idMatch = ID_REGEX.exec(lines[1]);
    if (!idMatch) {
        return null;
    }

    let sourcePath: string | undefined;
    for (let i = 2; i < Math.min(lines.length, 10); i++) {
        const sourceMatch = SOURCE_REGEX.exec(lines[i]);
        if (sourceMatch) {
            const val = sourceMatch[1].trim();
            if (!val.startsWith('!') && val !== 'null') {
                sourcePath = val;
            }
            break;
        }
    }

    return {
        id: idMatch[1].toLowerCase(),
        typeName: typeMatch[1],
        sourcePath,
    };
}

// Full parse: extracts all references, source paths, and entity definitions
export function parseAssetFull(content: string): ParsedAsset | null {
    const header = parseAssetHeader(content);
    if (!header) {
        return null;
    }

    const references = findAssetReferences(content);
    const sourcePaths = findSourcePaths(content);
    const parts = parseAssetParts(content);

    // Filter out the asset's own Id from references (it appears on line 1 sometimes)
    const filteredRefs = references.filter(r => r.guid !== header.id);

    return {
        ...header,
        references: filteredRefs,
        sourcePaths,
        parts,
    };
}

// Parse asset part definitions (entities in scenes/prefabs, UI elements in pages/libraries)
// Parts are defined as indented blocks with an Id: GUID field
const PART_ID_REGEX = /^(\s+)Id:\s+([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/i;
const PART_NAME_REGEX = /^\s+Name\*?:\s+(.+)/;

function parseAssetParts(content: string): AssetPartDefinition[] {
    const parts: AssetPartDefinition[] = [];
    const lines = content.split('\n');
    // The asset's own Id is on line 1 - skip it
    // Part Ids appear deeper in the hierarchy (indented)
    for (let i = 2; i < lines.length; i++) {
        const idMatch = PART_ID_REGEX.exec(lines[i]);
        if (idMatch && idMatch[1].length > 0) { // Must be indented (not the root Id)
            const part: AssetPartDefinition = {
                id: idMatch[2].toLowerCase(),
                line: i,
            };
            // Look ahead for a Name field among sibling properties.
            // Id: and Name: are at the same indentation level (siblings).
            // Stop when we hit a line at lesser indentation (left the parent block).
            const idIndent = idMatch[1].length;
            for (let j = i + 1; j < lines.length; j++) {
                const lineJ = lines[j];
                // Skip empty lines
                if (lineJ.trim().length === 0) {
                    continue;
                }
                // Measure indentation — if below the Id line's level, we've left the block
                const lineIndent = lineJ.length - lineJ.trimStart().length;
                if (lineIndent < idIndent) {
                    break;
                }
                const nameMatch = PART_NAME_REGEX.exec(lineJ);
                if (nameMatch) {
                    part.name = nameMatch[1].trim();
                    break;
                }
            }
            parts.push(part);
        }
    }
    return parts;
}

// Build an AssetEntry from a parsed header and file path
export function headerToAssetEntry(header: AssetHeader, filePath: string): AssetEntry {
    const ext = path.extname(filePath);
    const baseName = path.basename(filePath, ext);
    return {
        id: header.id,
        location: baseName,
        filePath,
        typeName: header.typeName,
        extension: ext,
        sourcePath: header.sourcePath,
    };
}
