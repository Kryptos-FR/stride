import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { AssetIndex } from '../core/assetIndex';
import { getAssetReferenceAtPosition, getPartReferenceAtPosition, getSourcePathAtPosition, getScriptReferenceAtPosition, getPropertyKeyAtPosition, findContainingScriptType } from '../core/referencePattern';

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

export class StrideHoverProvider implements vscode.HoverProvider {
    constructor(private index: AssetIndex) {}

    provideHover(
        document: vscode.TextDocument,
        position: vscode.Position,
        _token: vscode.CancellationToken
    ): vscode.Hover | undefined {
        const text = document.getText();
        const line = position.line;
        const col = position.character;

        // Check for cross-asset reference
        const assetRef = getAssetReferenceAtPosition(text, line, col);
        if (assetRef) {
            const entry = this.index.lookupGuid(assetRef.guid);
            if (entry) {
                const typeDesc = TYPE_DESCRIPTIONS[entry.typeName] ?? entry.typeName;
                const relativePath = vscode.workspace.asRelativePath(entry.filePath);
                const md = new vscode.MarkdownString();
                md.appendMarkdown(`**${typeDesc}** \`${entry.location}\`\n\n`);
                md.appendMarkdown(`- Type: ${entry.typeName} (${entry.extension})\n`);
                md.appendMarkdown(`- File: ${relativePath}\n`);
                if (entry.sourcePath) {
                    md.appendMarkdown(`- Source: ${entry.sourcePath}\n`);
                }
                md.appendMarkdown(`- ID: \`${entry.id}\`\n`);
                return new vscode.Hover(
                    md,
                    new vscode.Range(
                        new vscode.Position(line, assetRef.startColumn),
                        new vscode.Position(line, assetRef.endColumn)
                    )
                );
            } else {
                const md = new vscode.MarkdownString();
                md.appendMarkdown(`**Missing Asset** \`${assetRef.name}\`\n\n`);
                md.appendMarkdown(`ID \`${assetRef.guid}\` not found in workspace.\n`);
                return new vscode.Hover(
                    md,
                    new vscode.Range(
                        new vscode.Position(line, assetRef.startColumn),
                        new vscode.Position(line, assetRef.endColumn)
                    )
                );
            }
        }

        // Check for part reference
        const partRef = getPartReferenceAtPosition(text, line, col);
        if (partRef) {
            const part = this.index.lookupPart(partRef.guid);
            if (part) {
                const partLabel = getPartLabel(part.filePath);
                const md = new vscode.MarkdownString();
                md.appendMarkdown(`**${partLabel}** \`${part.name ?? partRef.guid}\`\n\n`);
                md.appendMarkdown(`- File: ${vscode.workspace.asRelativePath(part.filePath)}\n`);
                md.appendMarkdown(`- Line: ${part.line + 1}\n`);
                return new vscode.Hover(
                    md,
                    new vscode.Range(
                        new vscode.Position(line, partRef.startColumn),
                        new vscode.Position(line, partRef.endColumn)
                    )
                );
            }
        }

        // Check for source path
        const sourcePath = getSourcePathAtPosition(text, line, col);
        if (sourcePath) {
            const dir = path.dirname(document.uri.fsPath);
            const resolved = path.resolve(dir, sourcePath.path);
            const exists = fs.existsSync(resolved);
            const ext = path.extname(sourcePath.path).toLowerCase();
            const fileName = path.basename(sourcePath.path);

            const md = new vscode.MarkdownString();
            if (exists) {
                md.appendMarkdown(`**Resource** \`${fileName}\`\n\n`);
                md.appendMarkdown(`- Extension: ${ext}\n`);
                md.appendMarkdown(`- Path: ${sourcePath.path}\n`);
            } else {
                md.appendMarkdown(`**Missing Resource** \`${fileName}\`\n\n`);
                md.appendMarkdown(`File not found: ${sourcePath.path}\n`);
            }
            return new vscode.Hover(
                md,
                new vscode.Range(
                    new vscode.Position(line, sourcePath.startColumn),
                    new vscode.Position(line, sourcePath.endColumn)
                )
            );
        }

        // Check for script/component type reference
        const scriptNavigationEnabled = vscode.workspace.getConfiguration('strideAssets').get<boolean>('scriptNavigationEnabled', false);
        if (scriptNavigationEnabled) {
            const scriptRef = getScriptReferenceAtPosition(text, line, col);
            if (scriptRef) {
                const isLocal = this.index.hasProject(scriptRef.assemblyName);
                const className = scriptRef.typeName.split('.').pop() ?? scriptRef.typeName;
                const md = new vscode.MarkdownString();
                md.appendMarkdown(`**Script** \`${className}\`\n\n`);
                md.appendMarkdown(`- Type: ${scriptRef.typeName}\n`);
                md.appendMarkdown(`- Assembly: ${scriptRef.assemblyName}\n`);
                md.appendMarkdown(`- Status: ${isLocal ? 'Local project (Ctrl+click to navigate)' : 'Framework/NuGet type'}\n`);
                return new vscode.Hover(
                    md,
                    new vscode.Range(
                        new vscode.Position(line, scriptRef.startColumn),
                        new vscode.Position(line, scriptRef.endColumn)
                    )
                );
            }
        }

        // Check for property key -> C# member
        if (scriptNavigationEnabled) {
            const propKey = getPropertyKeyAtPosition(text, line, col);
            if (propKey) {
                const containingType = findContainingScriptType(text, line);
                if (containingType && this.index.hasProject(containingType.assemblyName)) {
                    const className = containingType.typeName.split('.').pop() ?? containingType.typeName;
                    const md = new vscode.MarkdownString();
                    md.appendMarkdown(`**Property** \`${propKey.key}\` on \`${className}\`\n\n`);
                    md.appendMarkdown(`- Type: ${containingType.typeName}\n`);
                    md.appendMarkdown(`- Ctrl+click to navigate to C# source\n`);
                    return new vscode.Hover(
                        md,
                        new vscode.Range(
                            new vscode.Position(line, propKey.startColumn),
                            new vscode.Position(line, propKey.endColumn)
                        )
                    );
                }
            }
        }

        // Check for Id: GUID definition line — show back-references
        const lineText = document.lineAt(line).text;
        const idMatch = ID_LINE_REGEX.exec(lineText);
        if (idMatch) {
            const guid = idMatch[2].toLowerCase();
            const backRefs = this.index.getBackRefs(guid);
            if (backRefs.length > 0) {
                const md = new vscode.MarkdownString();
                md.appendMarkdown(`**${backRefs.length} reference${backRefs.length > 1 ? 's' : ''}** to \`${guid}\`\n\n`);
                // Show up to 10 references
                const shown = backRefs.slice(0, 10);
                for (const ref of shown) {
                    const relPath = vscode.workspace.asRelativePath(ref.sourceFilePath);
                    const ctx = ref.context ? ` (${ref.context})` : '';
                    md.appendMarkdown(`- ${relPath}:${ref.line + 1}${ctx}\n`);
                }
                if (backRefs.length > 10) {
                    md.appendMarkdown(`\n... and ${backRefs.length - 10} more\n`);
                }
                // Range covers the GUID portion
                const guidStart = lineText.indexOf(idMatch[2]);
                return new vscode.Hover(
                    md,
                    new vscode.Range(
                        new vscode.Position(line, guidStart),
                        new vscode.Position(line, guidStart + idMatch[2].length)
                    )
                );
            }
        }

        return undefined;
    }
}

// Returns a user-friendly label for a part based on its containing file type
function getPartLabel(filePath: string): string {
    if (filePath.endsWith('.sduipage') || filePath.endsWith('.sduilib')) {
        return 'UIElement';
    }
    return 'Entity';
}
