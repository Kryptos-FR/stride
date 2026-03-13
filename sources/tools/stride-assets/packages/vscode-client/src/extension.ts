import * as vscode from 'vscode';
import * as path from 'path';
import {
    LanguageClient,
    LanguageClientOptions,
    ServerOptions,
    TransportKind,
} from 'vscode-languageclient/node';
import { registerCSharpBridge } from './csharpBridge';

let client: LanguageClient;
let statusBarItem: vscode.StatusBarItem;

export async function activate(context: vscode.ExtensionContext): Promise<void> {
    // Path to the server module
    const serverModule = context.asAbsolutePath(path.join('..', 'server', 'out', 'server.js'));

    const serverOptions: ServerOptions = {
        run: { module: serverModule, transport: TransportKind.stdio },
        debug: { module: serverModule, transport: TransportKind.stdio },
    };

    const clientOptions: LanguageClientOptions = {
        documentSelector: [{ language: 'stride-asset' }],
        synchronize: {
            configurationSection: 'strideAssets',
            fileEvents: vscode.workspace.createFileSystemWatcher('**/*.sd*'),
        },
    };

    client = new LanguageClient(
        'strideAssets',
        'Stride Asset Navigator',
        serverOptions,
        clientOptions,
    );

    // Start the client (and the server)
    await client.start();

    // Register the C# bridge for script navigation
    registerCSharpBridge(client);

    // Status bar
    statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 0);
    statusBarItem.text = '$(file-code) Stride Assets';
    statusBarItem.tooltip = 'Stride Asset Navigator is active';
    statusBarItem.command = 'strideAssets.findAsset';
    statusBarItem.show();
    context.subscriptions.push(statusBarItem);

    // Script navigation: check C# extension availability
    const scriptNavigationEnabled = vscode.workspace.getConfiguration('strideAssets').get<boolean>('scriptNavigationEnabled', false);
    if (scriptNavigationEnabled) {
        const csharpExt = vscode.extensions.getExtension('ms-dotnettools.csharp');
        if (!csharpExt) {
            vscode.window.showWarningMessage(
                'Stride: Script navigation requires the C# extension. Install "C#" (ms-dotnettools.csharp) for full functionality.'
            );
        }
    }

    // Register commands
    context.subscriptions.push(
        vscode.commands.registerCommand('strideAssets.rebuildIndex', async () => {
            statusBarItem.text = '$(loading~spin) Stride: Rebuilding...';
            // Send a custom notification to the server to rebuild
            await client.sendNotification('stride/rebuildIndex');
            statusBarItem.text = '$(file-code) Stride Assets';
            vscode.window.showInformationMessage('Stride: Asset index rebuilt.');
        }),

        vscode.commands.registerCommand('strideAssets.findAsset', async () => {
            // Use VS Code's built-in symbol picker, backed by the server's workspace/symbol
            await vscode.commands.executeCommand('workbench.action.showAllSymbols');
        }),
    );
}

export async function deactivate(): Promise<void> {
    if (client) {
        await client.stop();
    }
}
