import * as assert from 'assert';
import * as path from 'path';
import { parsePackageFile } from '../../core/packageParser';

const SAMPLE_PACKAGE = `!Package
SerializedVersion: {Assets: 3.1.0.0}
Meta:
    Name: SpaceEscape.Game
    Version: 1.0.0
    Authors: []
    Owners: []
    Dependencies: null
AssetFolders:
    -   Path: !dir ../Assets/Shared
    -   Path: !dir Effects
ResourceFolders:
    - !dir ../Resources
OutputGroupDirectories: {}
ExplicitFolders: []
Bundles: []
TemplateFolders: []
RootAssets: []`;

suite('Package Parser Tests', () => {
    test('parsePackageFile: extracts name', () => {
        const pkg = parsePackageFile(SAMPLE_PACKAGE, '/project/Game/Game.sdpkg');
        assert.strictEqual(pkg.name, 'SpaceEscape.Game');
    });

    test('parsePackageFile: extracts asset folders', () => {
        const pkg = parsePackageFile(SAMPLE_PACKAGE, '/project/Game/Game.sdpkg');
        assert.strictEqual(pkg.assetFolders.length, 2);
        // Paths should be resolved relative to the package file directory
        assert.ok(pkg.assetFolders[0].endsWith(path.join('project', 'Assets', 'Shared')));
        assert.ok(pkg.assetFolders[1].endsWith(path.join('project', 'Game', 'Effects')));
    });

    test('parsePackageFile: extracts resource folders', () => {
        const pkg = parsePackageFile(SAMPLE_PACKAGE, '/project/Game/Game.sdpkg');
        assert.strictEqual(pkg.resourceFolders.length, 1);
        assert.ok(pkg.resourceFolders[0].endsWith(path.join('project', 'Resources')));
    });

    test('parsePackageFile: empty package', () => {
        const content = `!Package
SerializedVersion: {Assets: 3.1.0.0}
Meta:
    Name: Empty
AssetFolders: []
ResourceFolders: []`;
        const pkg = parsePackageFile(content, '/project/Empty.sdpkg');
        assert.strictEqual(pkg.name, 'Empty');
        assert.strictEqual(pkg.assetFolders.length, 0);
        assert.strictEqual(pkg.resourceFolders.length, 0);
    });
});
