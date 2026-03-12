import * as vscode from 'vscode';

export interface AssetEntry {
    id: string;          // GUID (lowercase)
    location: string;    // Logical name (e.g., "BG_00 Texture")
    filePath: string;    // Absolute filesystem path
    typeName: string;    // From !tag (e.g., "MaterialAsset", "Model")
    extension: string;   // File extension (e.g., ".sdmat")
    sourcePath?: string; // Raw resource relative path, if present
}

export interface EntityEntry {
    id: string;          // Entity/component GUID (lowercase)
    filePath: string;    // Scene/prefab file containing this entity
    name?: string;       // Entity Name if available
    line: number;        // Line number for navigation
}

export interface BackReference {
    sourceFilePath: string;  // File containing the reference
    line: number;            // Line number of the reference
    column: number;          // Start column of the reference
    context: string;         // The YAML key or surrounding text for display
}

export class AssetIndex {
    private assetsByGuid = new Map<string, AssetEntry>();
    private guidsByFile = new Map<string, string[]>();
    private entitiesByGuid = new Map<string, EntityEntry>();
    private entitiesByFile = new Map<string, string[]>();

    // Back-reference index: target GUID -> list of locations referencing it
    private backRefs = new Map<string, BackReference[]>();
    private backRefsByFile = new Map<string, string[]>(); // source file -> target GUIDs it references

    // Project names from .csproj files (for script reference validation)
    private projectNames = new Set<string>();

    private _onDidUpdate = new vscode.EventEmitter<void>();
    readonly onDidUpdate = this._onDidUpdate.event;

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

        const entityIds = this.entitiesByFile.get(filePath);
        if (entityIds) {
            for (const id of entityIds) {
                this.entitiesByGuid.delete(id);
            }
            this.entitiesByFile.delete(filePath);
        }

        this.clearBackRefsFromFile(filePath);
    }

    updateFile(filePath: string, entries: AssetEntry[]): void {
        this.removeFile(filePath);
        for (const entry of entries) {
            this.addAsset(entry);
        }
        this._onDidUpdate.fire();
    }

    lookupGuid(guid: string): AssetEntry | undefined {
        return this.assetsByGuid.get(guid.toLowerCase());
    }

    getAllAssets(): AssetEntry[] {
        return Array.from(this.assetsByGuid.values());
    }

    addEntity(entry: EntityEntry): void {
        const id = entry.id.toLowerCase();
        entry.id = id;
        this.entitiesByGuid.set(id, entry);

        const fileEntities = this.entitiesByFile.get(entry.filePath) ?? [];
        if (!fileEntities.includes(id)) {
            fileEntities.push(id);
        }
        this.entitiesByFile.set(entry.filePath, fileEntities);
    }

    clearEntitiesForFile(filePath: string): void {
        const entityIds = this.entitiesByFile.get(filePath);
        if (entityIds) {
            for (const id of entityIds) {
                this.entitiesByGuid.delete(id);
            }
            this.entitiesByFile.delete(filePath);
        }
    }

    lookupEntity(guid: string): EntityEntry | undefined {
        return this.entitiesByGuid.get(guid.toLowerCase());
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
        this.entitiesByGuid.clear();
        this.entitiesByFile.clear();
        this.backRefs.clear();
        this.backRefsByFile.clear();
        this.projectNames.clear();
        this._onDidUpdate.fire();
    }

    dispose(): void {
        this._onDidUpdate.dispose();
    }
}
