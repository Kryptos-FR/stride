import * as vscode from 'vscode';

let outputChannel: vscode.OutputChannel | undefined;
let enabled = false;

// Initialize the logger. Automatically enables in Extension Development Host mode.
export function initLogger(context: vscode.ExtensionContext): void {
    enabled = context.extensionMode === vscode.ExtensionMode.Development;
    if (enabled) {
        outputChannel = vscode.window.createOutputChannel('Stride Assets');
        context.subscriptions.push(outputChannel);
        log('Logger initialized (development mode)');
    }
}

export function log(message: string): void {
    if (!enabled || !outputChannel) { return; }
    outputChannel.appendLine(`[${timestamp()}] ${message}`);
}

function timestamp(): string {
    const d = new Date();
    return `${d.getHours().toString().padStart(2, '0')}:${d.getMinutes().toString().padStart(2, '0')}:${d.getSeconds().toString().padStart(2, '0')}.${d.getMilliseconds().toString().padStart(3, '0')}`;
}
