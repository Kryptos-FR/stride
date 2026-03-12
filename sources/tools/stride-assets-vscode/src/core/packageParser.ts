import * as path from 'path';

export interface PackageInfo {
    name: string;
    packageFilePath: string;
    assetFolders: string[];     // Absolute paths to asset directories
    resourceFolders: string[];  // Absolute paths to resource directories
}

// Parse a .sdpkg file to extract AssetFolders and ResourceFolders
// Format:
//   AssetFolders:
//       -   Path: !dir ../Assets/Shared
//       -   Path: !dir Effects
//   ResourceFolders:
//       - !dir ../Resources
export function parsePackageFile(content: string, packageFilePath: string): PackageInfo {
    const packageDir = path.dirname(packageFilePath);
    const lines = content.split('\n');

    let name = '';
    const assetFolders: string[] = [];
    const resourceFolders: string[] = [];

    let section: 'none' | 'meta' | 'assetFolders' | 'resourceFolders' = 'none';

    for (const line of lines) {
        const trimmed = line.trimEnd();

        // Detect section headers
        if (/^Meta:/.test(trimmed)) {
            section = 'meta';
            continue;
        }
        if (/^AssetFolders:/.test(trimmed)) {
            section = 'assetFolders';
            continue;
        }
        if (/^ResourceFolders:/.test(trimmed)) {
            section = 'resourceFolders';
            continue;
        }
        // Any other top-level key ends current section
        if (/^\S/.test(trimmed) && trimmed.includes(':')) {
            section = 'none';
            continue;
        }

        if (section === 'meta') {
            const nameMatch = /^\s+Name:\s+(.+)/.exec(trimmed);
            if (nameMatch) {
                name = nameMatch[1].trim();
            }
        }

        if (section === 'assetFolders') {
            // Format: -   Path: !dir ../Assets/Shared
            const dirMatch = /Path:\s+!dir\s+(.+)/.exec(trimmed);
            if (dirMatch) {
                const dirPath = dirMatch[1].trim();
                assetFolders.push(path.resolve(packageDir, dirPath));
            }
        }

        if (section === 'resourceFolders') {
            // Format: - !dir ../Resources
            const dirMatch = /!dir\s+(.+)/.exec(trimmed);
            if (dirMatch) {
                const dirPath = dirMatch[1].trim();
                resourceFolders.push(path.resolve(packageDir, dirPath));
            }
        }
    }

    return {
        name,
        packageFilePath,
        assetFolders,
        resourceFolders,
    };
}
