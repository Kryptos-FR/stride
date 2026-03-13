import * as fs from 'fs';
import * as path from 'path';
import { HoverParams, Hover, MarkupKind, Range, Position } from 'vscode-languageserver';
import { TextDocument } from 'vscode-languageserver-textdocument';
import { AssetIndex } from '../assetIndex';
import {
    getAssetReferenceAtPosition,
    getPartReferenceAtPosition,
    getSourcePathAtPosition,
    getScriptReferenceAtPosition,
    getPropertyKeyAtPosition,
    findContainingScriptType,
} from 'stride-assets-core';

// Map type names to human-readable descriptions
const TYPE_DESCRIPTIONS: Record<string, string> = {
    'MaterialAsset': 'Material',
    'Texture': 'Texture',
    'Model': '3D Model',
    'Animation': 'Animation',
    'Skeleton': 'Skeleton',
    'SpriteSheet': 'Sprite Sheet',
    'SpriteFont': 'Sprite Font',
    'SceneAsset': 'Scene',
    'PrefabAsset': 'Prefab',
    'GraphicsCompositorAsset': 'Graphics Compositor',
    'GameSettingsAsset': 'Game Settings',
    'SkyboxAsset': 'Skybox',
    'Sound': 'Sound',
    'Video': 'Video',
    'NavigationMeshAsset': 'Navigation Mesh',
    'UIPageAsset': 'UI Page',
    'UILibraryAsset': 'UI Library',
    'EffectShader': 'Shader',
    'RenderTexture': 'Render Texture',
    'ProceduralModelAsset': 'Procedural Model',
    'PrefabModelAsset': 'Prefab Model',
    'Package': 'Package',
};

// Matches an Id: GUID line (both asset-level and part-level)
const ID_LINE_REGEX = /^(\s*)Id:\s+([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/i;

interface Settings {
    scriptNavigationEnabled: boolean;
}

export function createHoverHandler(index: AssetIndex, getSettings: () => Settings, connection?: { console: { log(msg: string): void } }) {
    function log(msg: string): void {
        connection?.console.log(`[Stride:Hover] ${msg}`);
    }

    return {
        handle(params: HoverParams, doc: TextDocument | undefined): Hover | null {
            if (!doc) { return null; }

            const text = doc.getText();
            const line = params.position.line;
            const col = params.position.character;
            const filePath = uriToPath(doc.uri);

            // Check for cross-asset reference
            const assetRef = getAssetReferenceAtPosition(text, line, col);
            if (assetRef) {
                const entry = index.lookupGuid(assetRef.guid);
                if (entry) {
                    const typeDesc = TYPE_DESCRIPTIONS[entry.typeName] ?? entry.typeName;
                    const relativePath = path.relative(path.dirname(filePath), entry.filePath);
                    const lines: string[] = [
                        `**${typeDesc}** \`${entry.location}\`\n`,
                        `- Type: ${entry.typeName} (${entry.extension})`,
                        `- File: ${relativePath}`,
                    ];
                    if (entry.sourcePath) {
                        lines.push(`- Source: ${entry.sourcePath}`);
                    }
                    lines.push(`- ID: \`${entry.id}\``);
                    return {
                        contents: { kind: MarkupKind.Markdown, value: lines.join('\n') },
                        range: Range.create(Position.create(line, assetRef.startColumn), Position.create(line, assetRef.endColumn)),
                    };
                } else {
                    return {
                        contents: { kind: MarkupKind.Markdown, value: `**Missing Asset** \`${assetRef.name}\`\n\nID \`${assetRef.guid}\` not found in workspace.` },
                        range: Range.create(Position.create(line, assetRef.startColumn), Position.create(line, assetRef.endColumn)),
                    };
                }
            }

            // Check for part reference
            const partRef = getPartReferenceAtPosition(text, line, col);
            if (partRef) {
                const part = index.lookupPart(partRef.guid);
                if (part) {
                    const partLabel = getPartLabel(part.filePath);
                    const relativePath = path.relative(path.dirname(filePath), part.filePath);
                    return {
                        contents: { kind: MarkupKind.Markdown, value: `**${partLabel}** \`${part.name ?? partRef.guid}\`\n\n- File: ${relativePath}\n- Line: ${part.line + 1}` },
                        range: Range.create(Position.create(line, partRef.startColumn), Position.create(line, partRef.endColumn)),
                    };
                }
            }

            // Check for source path
            const sourcePath = getSourcePathAtPosition(text, line, col);
            if (sourcePath) {
                const dir = path.dirname(filePath);
                const resolved = path.resolve(dir, sourcePath.path);
                const exists = fs.existsSync(resolved);
                const ext = path.extname(sourcePath.path).toLowerCase();
                const fileName = path.basename(sourcePath.path);

                const md = exists
                    ? `**Resource** \`${fileName}\`\n\n- Extension: ${ext}\n- Path: ${sourcePath.path}`
                    : `**Missing Resource** \`${fileName}\`\n\nFile not found: ${sourcePath.path}`;
                return {
                    contents: { kind: MarkupKind.Markdown, value: md },
                    range: Range.create(Position.create(line, sourcePath.startColumn), Position.create(line, sourcePath.endColumn)),
                };
            }

            // Check for script/component type reference
            const settings = getSettings();
            if (settings.scriptNavigationEnabled) {
                const scriptRef = getScriptReferenceAtPosition(text, line, col);
                if (scriptRef) {
                    const isLocal = index.hasProject(scriptRef.assemblyName);
                    const className = scriptRef.typeName.split('.').pop() ?? scriptRef.typeName;
                    return {
                        contents: { kind: MarkupKind.Markdown, value: `**Script** \`${className}\`\n\n- Type: ${scriptRef.typeName}\n- Assembly: ${scriptRef.assemblyName}\n- Status: ${isLocal ? 'Local project (Ctrl+click to navigate)' : 'Framework/NuGet type'}` },
                        range: Range.create(Position.create(line, scriptRef.startColumn), Position.create(line, scriptRef.endColumn)),
                    };
                }

                // Check for property key -> C# member
                const propKey = getPropertyKeyAtPosition(text, line, col);
                if (propKey) {
                    const containingType = findContainingScriptType(text, line);
                    if (containingType && index.hasProject(containingType.assemblyName)) {
                        const className = containingType.typeName.split('.').pop() ?? containingType.typeName;
                        return {
                            contents: { kind: MarkupKind.Markdown, value: `**Property** \`${propKey.key}\` on \`${className}\`\n\n- Type: ${containingType.typeName}\n- Ctrl+click to navigate to C# source` },
                            range: Range.create(Position.create(line, propKey.startColumn), Position.create(line, propKey.endColumn)),
                        };
                    }
                }
            }

            // Check for Id: GUID definition line — show back-references
            const lines = text.split('\n');
            const lineText = lines[line] ?? '';
            const idMatch = ID_LINE_REGEX.exec(lineText);
            if (idMatch) {
                const guid = idMatch[2].toLowerCase();
                const backRefs = index.getBackRefs(guid);
                if (backRefs.length > 0) {
                    const mdLines: string[] = [`**${backRefs.length} reference${backRefs.length > 1 ? 's' : ''}** to \`${guid}\`\n`];
                    const shown = backRefs.slice(0, 10);
                    for (const ref of shown) {
                        const relPath = path.relative(path.dirname(filePath), ref.sourceFilePath);
                        const ctx = ref.context ? ` (${ref.context})` : '';
                        mdLines.push(`- ${relPath}:${ref.line + 1}${ctx}`);
                    }
                    if (backRefs.length > 10) {
                        mdLines.push(`\n... and ${backRefs.length - 10} more`);
                    }
                    const guidStart = lineText.indexOf(idMatch[2]);
                    return {
                        contents: { kind: MarkupKind.Markdown, value: mdLines.join('\n') },
                        range: Range.create(Position.create(line, guidStart), Position.create(line, guidStart + idMatch[2].length)),
                    };
                }
            }

            return null;
        },
    };
}

function getPartLabel(filePath: string): string {
    if (filePath.endsWith('.sduipage') || filePath.endsWith('.sduilib')) {
        return 'UIElement';
    }
    return 'Entity';
}

function uriToPath(uri: string): string {
    const url = new URL(uri);
    return decodeURIComponent(url.pathname).replace(/^\/([A-Za-z]:)/, '$1');
}
