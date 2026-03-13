// Shared types used by both the LSP server and IDE clients

export interface AssetEntry {
    id: string;          // GUID (lowercase)
    location: string;    // Logical name (e.g., "BG_00 Texture")
    filePath: string;    // Absolute filesystem path
    typeName: string;    // From !tag (e.g., "MaterialAsset", "Model")
    extension: string;   // File extension (e.g., ".sdmat")
    sourcePath?: string; // Raw resource relative path, if present
}

export interface AssetPartEntry {
    id: string;          // Part GUID (lowercase)
    filePath: string;    // File containing this part (scene, prefab, UI page, etc.)
    name?: string;       // Part name if available
    line: number;        // Line number for navigation
}

export interface BackReference {
    sourceFilePath: string;  // File containing the reference
    line: number;            // Line number of the reference
    column: number;          // Start column of the reference
    context: string;         // The YAML key or surrounding text for display
}
