import { WorkspaceSymbolParams, SymbolInformation, SymbolKind, Location, Range, Position } from 'vscode-languageserver';
import { AssetIndex } from '../assetIndex';

export function createSymbolHandler(index: AssetIndex) {
    return {
        handle(params: WorkspaceSymbolParams): SymbolInformation[] {
            const query = params.query.toLowerCase();
            const assets = index.getAllAssets();

            return assets
                .filter(a => a.location.toLowerCase().includes(query))
                .slice(0, 50) // Limit results
                .map(a => SymbolInformation.create(
                    a.location,
                    SymbolKind.File,
                    Range.create(Position.create(0, 0), Position.create(0, 0)),
                    pathToUri(a.filePath),
                    a.typeName,
                ));
        },
    };
}

function pathToUri(filePath: string): string {
    const normalized = filePath.replace(/\\/g, '/');
    if (/^[A-Za-z]:/.test(normalized)) {
        return `file:///${normalized}`;
    }
    return `file://${normalized}`;
}
