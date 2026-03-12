import * as vscode from 'vscode';
import { AssetIndex } from './core/assetIndex';
import { WorkspaceScanner } from './core/workspaceScanner';
import { parseAssetFull } from './core/assetParser';
import { StrideDefinitionProvider } from './providers/definitionProvider';
import { StrideHoverProvider } from './providers/hoverProvider';
import { StrideDocumentLinkProvider } from './providers/documentLinkProvider';
import { StrideDiagnosticsProvider } from './providers/diagnosticsProvider';

const LANGUAGE_SELECTOR: vscode.DocumentSelector = { language: 'stride-asset' };

let statusBarItem: vscode.StatusBarItem;

export async function activate(context: vscode.ExtensionContext): Promise<void> {
    const index = new AssetIndex();
    const scanner = new WorkspaceScanner(index);

    // Status bar
    statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 0);
    statusBarItem.text = '$(loading~spin) Stride: Indexing...';
    statusBarItem.show();
    context.subscriptions.push(statusBarItem);

    // Run initial scan with progress
    await vscode.window.withProgress(
        {
            location: vscode.ProgressLocation.Window,
            title: 'Stride: Indexing assets',
        },
        async (progress) => {
            await scanner.initialize(progress);
        }
    );

    updateStatusBar(index);
    index.onDidUpdate(() => updateStatusBar(index));

    // Register providers
    const definitionProvider = new StrideDefinitionProvider(index);
    const hoverProvider = new StrideHoverProvider(index);
    const linkProvider = new StrideDocumentLinkProvider(index);
    const diagnosticsProvider = new StrideDiagnosticsProvider(index);

    context.subscriptions.push(
        vscode.languages.registerDefinitionProvider(LANGUAGE_SELECTOR, definitionProvider),
        vscode.languages.registerHoverProvider(LANGUAGE_SELECTOR, hoverProvider),
        vscode.languages.registerDocumentLinkProvider(LANGUAGE_SELECTOR, linkProvider),
        diagnosticsProvider,
        scanner,
        index,
    );

    // Index entities for already-open scene/prefab files
    for (const editor of vscode.window.visibleTextEditors) {
        await indexEntitiesIfNeeded(editor.document, index);
    }

    // Index entities when a scene/prefab is opened
    context.subscriptions.push(
        vscode.workspace.onDidOpenTextDocument(async (doc) => {
            await indexEntitiesIfNeeded(doc, index);
        })
    );

    // Register commands
    context.subscriptions.push(
        vscode.commands.registerCommand('strideAssets.rebuildIndex', async () => {
            statusBarItem.text = '$(loading~spin) Stride: Rebuilding...';
            index.clear();
            await scanner.initialize();
            updateStatusBar(index);
            vscode.window.showInformationMessage(`Stride: Rebuilt index with ${index.size} assets.`);
        }),

        vscode.commands.registerCommand('strideAssets.findAsset', async () => {
            const assets = index.getAllAssets();
            const items = assets.map(a => ({
                label: a.location,
                description: `${a.typeName} (${a.extension})`,
                detail: vscode.workspace.asRelativePath(a.filePath),
                asset: a,
            }));

            const selected = await vscode.window.showQuickPick(items, {
                placeHolder: 'Search for an asset by name...',
                matchOnDescription: true,
                matchOnDetail: true,
            });

            if (selected) {
                const doc = await vscode.workspace.openTextDocument(selected.asset.filePath);
                await vscode.window.showTextDocument(doc);
            }
        })
    );

    // Optionally scan workspace for broken links on startup
    const scanOnStartup = vscode.workspace.getConfiguration('strideAssets').get<boolean>('scanWorkspaceForBrokenLinks', false);
    if (scanOnStartup) {
        await diagnosticsProvider.scanWorkspace();
    }
}

async function indexEntitiesIfNeeded(doc: vscode.TextDocument, index: AssetIndex): Promise<void> {
    const fsPath = doc.uri.fsPath;
    if (fsPath.endsWith('.sdscene') || fsPath.endsWith('.sdprefab')) {
        const parsed = parseAssetFull(doc.getText());
        if (parsed) {
            index.clearEntitiesForFile(fsPath);
            for (const entity of parsed.entities) {
                index.addEntity({
                    id: entity.id,
                    filePath: fsPath,
                    name: entity.name,
                    line: entity.line,
                });
            }
        }
    }
}

function updateStatusBar(index: AssetIndex): void {
    statusBarItem.text = `$(file-code) Stride: ${index.size} assets`;
    statusBarItem.tooltip = 'Click to find an asset by name';
    statusBarItem.command = 'strideAssets.findAsset';
}

export function deactivate(): void {
    // Cleanup handled by disposables
}
