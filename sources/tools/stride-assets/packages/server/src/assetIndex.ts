import { EventEmitter } from 'events';
import { AssetEntry, AssetPartEntry, BackReference } from 'stride-assets-core';

export class AssetIndex {
    private assetsByGuid = new Map<string, AssetEntry>();
    private guidsByFile = new Map<string, string[]>();
    private partsByGuid = new Map<string, AssetPartEntry>();
    private partsByFile = new Map<string, string[]>();

    // Back-reference index: target GUID -> list of locations referencing it
    private backRefs = new Map<string, BackReference[]>();
    private backRefsByFile = new Map<string, string[]>(); // source file -> target GUIDs it references

    // Project names from .csproj files (for script reference validation)
    private projectNames = new Set<string>();

    private emitter = new EventEmitter();

    onDidUpdate(listener: () => void): void {
        this.emitter.on('update', listener);
    }

    get size(): number {
        return this.assetsByGuid.size;
    }

    addAsset(entry: AssetEntry): void {
        const id = entry.id.toLowerCase();
        entry.id = id;
        this.assetsByGuid.set(id, entry);

        const fileGuids = this.guidsByFile.get(entry.filePath) ?? [];
        if (!fileGuids.includes(id)) {
            fileGuids.push(id);
        }
        this.guidsByFile.set(entry.filePath, fileGuids);
    }

    removeFile(filePath: string): void {
        const guids = this.guidsByFile.get(filePath);
        if (guids) {
            for (const guid of guids) {
                this.assetsByGuid.delete(guid);
            }
            this.guidsByFile.delete(filePath);
        }

        const partIds = this.partsByFile.get(filePath);
        if (partIds) {
            for (const id of partIds) {
                this.partsByGuid.delete(id);
            }
            this.partsByFile.delete(filePath);
        }

        this.clearBackRefsFromFile(filePath);
    }

    updateFile(filePath: string, entries: AssetEntry[]): void {
        this.removeFile(filePath);
        for (const entry of entries) {
            this.addAsset(entry);
        }
        this.emitter.emit('update');
    }

    lookupGuid(guid: string): AssetEntry | undefined {
        return this.assetsByGuid.get(guid.toLowerCase());
    }

    getAllAssets(): AssetEntry[] {
        return Array.from(this.assetsByGuid.values());
    }

    addPart(entry: AssetPartEntry): void {
        const id = entry.id.toLowerCase();
        entry.id = id;
        this.partsByGuid.set(id, entry);

        const fileParts = this.partsByFile.get(entry.filePath) ?? [];
        if (!fileParts.includes(id)) {
            fileParts.push(id);
        }
        this.partsByFile.set(entry.filePath, fileParts);
    }

    clearPartsForFile(filePath: string): void {
        const partIds = this.partsByFile.get(filePath);
        if (partIds) {
            for (const id of partIds) {
                this.partsByGuid.delete(id);
            }
            this.partsByFile.delete(filePath);
        }
    }

    lookupPart(guid: string): AssetPartEntry | undefined {
        return this.partsByGuid.get(guid.toLowerCase());
    }

    // --- Back-references (opt-in) ---

    addBackRef(targetGuid: string, ref: BackReference): void {
        const id = targetGuid.toLowerCase();
        const refs = this.backRefs.get(id) ?? [];
        refs.push(ref);
        this.backRefs.set(id, refs);

        // Track which target GUIDs are referenced from this source file
        const fileTargets = this.backRefsByFile.get(ref.sourceFilePath) ?? [];
        if (!fileTargets.includes(id)) {
            fileTargets.push(id);
        }
        this.backRefsByFile.set(ref.sourceFilePath, fileTargets);
    }

    clearBackRefsFromFile(filePath: string): void {
        const targetGuids = this.backRefsByFile.get(filePath);
        if (targetGuids) {
            for (const guid of targetGuids) {
                const refs = this.backRefs.get(guid);
                if (refs) {
                    const filtered = refs.filter(r => r.sourceFilePath !== filePath);
                    if (filtered.length > 0) {
                        this.backRefs.set(guid, filtered);
                    } else {
                        this.backRefs.delete(guid);
                    }
                }
            }
            this.backRefsByFile.delete(filePath);
        }
    }

    getBackRefs(guid: string): BackReference[] {
        return this.backRefs.get(guid.toLowerCase()) ?? [];
    }

    get backRefCount(): number {
        let count = 0;
        for (const refs of this.backRefs.values()) {
            count += refs.length;
        }
        return count;
    }

    // --- Project names (for script navigation) ---

    setProjectNames(names: Set<string>): void {
        this.projectNames = names;
    }

    hasProject(name: string): boolean {
        return this.projectNames.has(name);
    }

    clear(): void {
        this.assetsByGuid.clear();
        this.guidsByFile.clear();
        this.partsByGuid.clear();
        this.partsByFile.clear();
        this.backRefs.clear();
        this.backRefsByFile.clear();
        this.projectNames.clear();
        this.emitter.emit('update');
    }
}
