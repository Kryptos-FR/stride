import * as fs from 'fs';
import * as path from 'path';
import { DocumentLinkParams, DocumentLink, Range, Position } from 'vscode-languageserver';
import { TextDocument } from 'vscode-languageserver-textdocument';
import { AssetIndex } from '../assetIndex';
import { findAssetReferences, findSourcePaths } from 'stride-assets-core';

export function createDocumentLinkHandler(index: AssetIndex) {
    return {
        handle(params: DocumentLinkParams, doc: TextDocument | undefined): DocumentLink[] {
            if (!doc) { return []; }

            const text = doc.getText();
            const links: DocumentLink[] = [];
            const filePath = uriToPath(doc.uri);

            // Asset references (GUID:Name) -> link to the asset file
            const assetRefs = findAssetReferences(text);
            for (const ref of assetRefs) {
                const entry = index.lookupGuid(ref.guid);
                if (entry) {
                    links.push({
                        range: Range.create(Position.create(ref.line, ref.startColumn), Position.create(ref.line, ref.endColumn)),
                        target: pathToUri(entry.filePath),
                        tooltip: `Open ${entry.typeName}: ${entry.location}`,
                    });
                }
            }

            // Source paths -> file URI link
            const sourcePaths = findSourcePaths(text);
            for (const sp of sourcePaths) {
                const dir = path.dirname(filePath);
                const resolved = path.resolve(dir, sp.path);
                const exists = fs.existsSync(resolved);
                links.push({
                    range: Range.create(Position.create(sp.line, sp.startColumn), Position.create(sp.line, sp.endColumn)),
                    target: pathToUri(resolved),
                    tooltip: exists
                        ? `Open resource: ${path.basename(sp.path)}`
                        : `Source file missing: ${sp.path}`,
                });
            }

            return links;
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

function uriToPath(uri: string): string {
    const url = new URL(uri);
    return decodeURIComponent(url.pathname).replace(/^\/([A-Za-z]:)/, '$1');
}
