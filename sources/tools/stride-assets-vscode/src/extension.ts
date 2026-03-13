import * as vscode from 'vscode';
import { AssetIndex } from './core/assetIndex';
import { WorkspaceScanner } from './core/workspaceScanner';
import { BackLinkScanner } from './core/backLinkScanner';
import { parseAssetFull } from './core/assetParser';
import { StrideDefinitionProvider } from './providers/definitionProvider';
import { StrideHoverProvider } from './providers/hoverProvider';
import { StrideDocumentLinkProvider } from './providers/documentLinkProvider';
import { StrideDiagnosticsProvider } from './providers/diagnosticsProvider';
import { StrideReferencesCodeLensProvider } from './providers/referencesCodeLensProvider';
import { initLogger } from './core/logger';

const LANGUAGE_SELECTOR: vscode.DocumentSelector = { language: 'stride-asset' };

let statusBarItem: vscode.StatusBarItem;

export async function activate(context: vscode.ExtensionContext): Promise<void> {
    initLogger(context);
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

    // Index parts for already-open composite asset files
    for (const editor of vscode.window.visibleTextEditors) {
        await indexPartsIfNeeded(editor.document, index);
    }

    // Index parts when a composite asset is opened
    context.subscriptions.push(
        vscode.workspace.onDidOpenTextDocument(async (doc) => {
            await indexPartsIfNeeded(doc, index);
        })
    );

    // Script navigation (opt-in): check C# extension availability
    const scriptNavigationEnabled = vscode.workspace.getConfiguration('strideAssets').get<boolean>('scriptNavigationEnabled', false);
    if (scriptNavigationEnabled) {
        const csharpExt = vscode.extensions.getExtension('ms-dotnettools.csharp');
        if (!csharpExt) {
            vscode.window.showWarningMessage(
                'Stride: Script navigation requires the C# extension. Install "C#" (ms-dotnettools.csharp) for full functionality.'
            );
        }
    }

    // Back-links (opt-in): scan all files for references, enable CodeLens
    const backLinksEnabled = vscode.workspace.getConfiguration('strideAssets').get<boolean>('backLinksEnabled', false);
    if (backLinksEnabled) {
        await enableBackLinks(context, index);
    }

    // Register commands
    context.subscriptions.push(
        vscode.commands.registerCommand('strideAssets.rebuildIndex', async () => {
            statusBarItem.text = '$(loading~spin) Stride: Rebuilding...';
            index.clear();
            await scanner.initialize();
            const backLinks = vscode.workspace.getConfiguration('strideAssets').get<boolean>('backLinksEnabled', false);
            if (backLinks) {
                await enableBackLinks(context, index);
            }
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

async function enableBackLinks(context: vscode.ExtensionContext, index: AssetIndex): Promise<void> {
    const backLinkScanner = new BackLinkScanner(index);

    await vscode.window.withProgress(
        {
            location: vscode.ProgressLocation.Window,
            title: 'Stride: Scanning back-links',
        },
        async (progress) => {
            await backLinkScanner.scanWorkspace(progress);
        }
    );

    // Set up file watcher for incremental back-link updates
    backLinkScanner.setupFileWatcher();

    // Register CodeLens provider
    const codeLensProvider = new StrideReferencesCodeLensProvider(index);
    context.subscriptions.push(
        vscode.languages.registerCodeLensProvider(
            { language: 'stride-asset' },
            codeLensProvider
        ),
        backLinkScanner,
    );
}

async function indexPartsIfNeeded(doc: vscode.TextDocument, index: AssetIndex): Promise<void> {
    const fsPath = doc.uri.fsPath;
    if (fsPath.endsWith('.sdscene') || fsPath.endsWith('.sdprefab') || fsPath.endsWith('.sduipage') || fsPath.endsWith('.sduilib')) {
        const parsed = parseAssetFull(doc.getText());
        if (parsed) {
            index.clearPartsForFile(fsPath);
            for (const part of parsed.parts) {
                index.addPart({
                    id: part.id,
                    filePath: fsPath,
                    name: part.name,
                    line: part.line,
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
