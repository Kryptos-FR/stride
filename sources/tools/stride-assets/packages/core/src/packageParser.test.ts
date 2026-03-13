import { describe, it, expect } from 'vitest';
import * as path from 'path';
import { parsePackageFile } from './packageParser';

describe('parsePackageFile', () => {
    const packageFilePath = '/project/SpaceEscape.Game/SpaceEscape.Game.sdpkg';

    it('parses package name from Meta section', () => {
        const content = [
            'Meta:',
            '    Name: SpaceEscape.Game',
            'AssetFolders:',
            '    -   Path: !dir ../Assets/Shared',
        ].join('\n');
        const info = parsePackageFile(content, packageFilePath);
        expect(info.name).toBe('SpaceEscape.Game');
    });

    it('parses asset folders as absolute paths', () => {
        const content = [
            'Meta:',
            '    Name: MyPackage',
            'AssetFolders:',
            '    -   Path: !dir ../Assets/Shared',
            '    -   Path: !dir Effects',
        ].join('\n');
        const info = parsePackageFile(content, packageFilePath);
        expect(info.assetFolders).toHaveLength(2);
        expect(info.assetFolders[0]).toBe(path.resolve('/project/SpaceEscape.Game', '../Assets/Shared'));
        expect(info.assetFolders[1]).toBe(path.resolve('/project/SpaceEscape.Game', 'Effects'));
    });

    it('parses resource folders', () => {
        const content = [
            'ResourceFolders:',
            '    - !dir ../Resources',
        ].join('\n');
        const info = parsePackageFile(content, packageFilePath);
        expect(info.resourceFolders).toHaveLength(1);
        expect(info.resourceFolders[0]).toBe(path.resolve('/project/SpaceEscape.Game', '../Resources'));
    });

    it('handles empty package file', () => {
        const info = parsePackageFile('', packageFilePath);
        expect(info.name).toBe('');
        expect(info.assetFolders).toHaveLength(0);
        expect(info.resourceFolders).toHaveLength(0);
    });

    it('preserves packageFilePath', () => {
        const info = parsePackageFile('', packageFilePath);
        expect(info.packageFilePath).toBe(packageFilePath);
    });

    it('stops section at next top-level key', () => {
        const content = [
            'AssetFolders:',
            '    -   Path: !dir Assets',
            'SomeOtherSection:',
            '    -   Path: !dir ShouldNotBeIncluded',
        ].join('\n');
        const info = parsePackageFile(content, packageFilePath);
        expect(info.assetFolders).toHaveLength(1);
    });

    it('parses a realistic .sdpkg file', () => {
        const content = [
            '!Package',
            'Id: 7e2b0b8a-f4c9-4fc0-9b38-e7d389a72fa4',
            'Meta:',
            '    Name: SpaceEscape.Game',
            '    Version: {Major: 1, Minor: 0, Patch: 0}',
            'AssetFolders:',
            '    -   Path: !dir ../Assets/Shared',
            '    -   Path: !dir Effects',
            'ResourceFolders:',
            '    - !dir ../Resources',
            'ExplicitFolders: []',
        ].join('\n');
        const info = parsePackageFile(content, packageFilePath);
        expect(info.name).toBe('SpaceEscape.Game');
        expect(info.assetFolders).toHaveLength(2);
        expect(info.resourceFolders).toHaveLength(1);
    });
});
