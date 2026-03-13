import {
    createConnection,
    ProposedFeatures,
    InitializeParams,
    InitializeResult,
    TextDocumentSyncKind,
    DidChangeConfigurationNotification,
} from 'vscode-languageserver/node';
import { TextDocument } from 'vscode-languageserver-textdocument';
import { AssetIndex } from './assetIndex';
import { WorkspaceScanner } from './workspaceScanner';
import { BackLinkScanner } from './backLinkScanner';
import { createDefinitionHandler } from './handlers/definitionHandler';
import { createHoverHandler } from './handlers/hoverHandler';
import { createDiagnosticsHandler } from './handlers/diagnosticsHandler';
import { createDocumentLinkHandler } from './handlers/documentLinkHandler';
import { createCodeLensHandler } from './handlers/codeLensHandler';
import { createSymbolHandler } from './handlers/symbolHandler';

// Create the LSP connection (stdio transport)
const connection = createConnection(ProposedFeatures.all);

// In-memory document store
const documents = new Map<string, TextDocument>();

// Core state
const index = new AssetIndex();
let scanner: WorkspaceScanner;
let backLinkScanner: BackLinkScanner;

// Settings
interface StrideSettings {
    diagnosticsEnabled: boolean;
    scriptNavigationEnabled: boolean;
    backLinksEnabled: boolean;
    scanWorkspaceForBrokenLinks: boolean;
}

let settings: StrideSettings = {
    diagnosticsEnabled: true,
    scriptNavigationEnabled: false,
    backLinksEnabled: false,
    scanWorkspaceForBrokenLinks: false,
};

connection.onInitialize((params: InitializeParams): InitializeResult => {
    const workspaceFolders = (params.workspaceFolders ?? []).map(f => {
        // Convert URI to file path
        const url = new URL(f.uri);
        return decodeURIComponent(url.pathname.replace(/^\/([A-Za-z]:)/, '$1'));
    });

    scanner = new WorkspaceScanner(index, workspaceFolders);
    backLinkScanner = new BackLinkScanner(index, workspaceFolders);

    return {
        capabilities: {
            textDocumentSync: TextDocumentSyncKind.Full,
            definitionProvider: true,
            hoverProvider: true,
            documentLinkProvider: { resolveProvider: false },
            codeLensProvider: { resolveProvider: false },
            workspaceSymbolProvider: true,
        },
    };
});

connection.onInitialized(async () => {
    // Register for configuration changes
    connection.client.register(DidChangeConfigurationNotification.type);

    // Scan workspace
    connection.console.log('Stride: Indexing assets...');
    await scanner.initialize((progress) => {
        connection.console.log(progress.message);
    });
    connection.console.log(`Stride: Indexed ${index.size} assets`);

    // Fetch initial settings
    await refreshSettings();

    // Enable back-links if configured
    if (settings.backLinksEnabled) {
        await backLinkScanner.scanWorkspace((msg) => connection.console.log(msg));
        backLinkScanner.setupFileWatcher();
    }
});

async function refreshSettings(): Promise<void> {
    try {
        const config = await connection.workspace.getConfiguration('strideAssets');
        if (config) {
            settings = {
                diagnosticsEnabled: config.diagnosticsEnabled ?? true,
                scriptNavigationEnabled: config.scriptNavigationEnabled ?? false,
                backLinksEnabled: config.backLinksEnabled ?? false,
                scanWorkspaceForBrokenLinks: config.scanWorkspaceForBrokenLinks ?? false,
            };
        }
    } catch {
        // Use defaults
    }
}

connection.onDidChangeConfiguration(async () => {
    await refreshSettings();
});

// --- Document synchronization ---

connection.onDidOpenTextDocument((params) => {
    const doc = TextDocument.create(
        params.textDocument.uri,
        params.textDocument.languageId,
        params.textDocument.version,
        params.textDocument.text,
    );
    documents.set(params.textDocument.uri, doc);

    // Index parts for composite assets
    const filePath = uriToPath(params.textDocument.uri);
    scanner.indexPartsFromContent(filePath, params.textDocument.text);

    // Run diagnostics
    if (settings.diagnosticsEnabled) {
        diagnosticsHandler.onDocumentChange(doc);
    }
});

connection.onDidChangeTextDocument((params) => {
    const existing = documents.get(params.textDocument.uri);
    if (existing) {
        const doc = TextDocument.update(existing, params.contentChanges, params.textDocument.version);
        documents.set(params.textDocument.uri, doc);

        // Re-index parts
        const filePath = uriToPath(params.textDocument.uri);
        scanner.indexPartsFromContent(filePath, doc.getText());

        // Run diagnostics
        if (settings.diagnosticsEnabled) {
            diagnosticsHandler.onDocumentChange(doc);
        }
    }
});

connection.onDidCloseTextDocument((params) => {
    documents.delete(params.textDocument.uri);
    diagnosticsHandler.onDocumentClose(params.textDocument.uri);
});

// --- Register handlers ---

const getSettings = () => settings;
const getDocument = (uri: string) => documents.get(uri);

// Definition (Ctrl+click)
const definitionHandler = createDefinitionHandler(index, connection, getSettings);
connection.onDefinition((params) => definitionHandler.handle(params, getDocument(params.textDocument.uri)));

// Hover tooltips
const hoverHandler = createHoverHandler(index, getSettings);
connection.onHover((params) => hoverHandler.handle(params, getDocument(params.textDocument.uri)));

// Diagnostics
const diagnosticsHandler = createDiagnosticsHandler(index, connection, getSettings);
index.onDidUpdate(() => diagnosticsHandler.onIndexUpdate(documents));

// Document links
const documentLinkHandler = createDocumentLinkHandler(index);
connection.onDocumentLinks((params) => documentLinkHandler.handle(params, getDocument(params.textDocument.uri)));

// CodeLens
const codeLensHandler = createCodeLensHandler(index);
connection.onCodeLens((params) => codeLensHandler.handle(params, getDocument(params.textDocument.uri)));

// Workspace symbol (find asset)
const symbolHandler = createSymbolHandler(index);
connection.onWorkspaceSymbol((params) => symbolHandler.handle(params));

// --- Utility ---

function uriToPath(uri: string): string {
    const url = new URL(uri);
    return decodeURIComponent(url.pathname.replace(/^\/([A-Za-z]:)/, '$1'));
}

// Start listening
connection.listen();
