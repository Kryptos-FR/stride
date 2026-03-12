import * as assert from 'assert';
import { parseAssetHeader, parseAssetFull, headerToAssetEntry } from '../../core/assetParser';

const SAMPLE_MODEL = `!Model
Id: f16bfc07-e816-475d-a400-304b8042d6c7
SerializedVersion: {Stride: 2.0.0.0}
Tags: []
Source: ../../Resources/model/BG/scene/bg_00.FBX
PivotPosition: {X: 0.0, Y: 0.0, Z: 0.0}
Materials:
    48b7a82aea9a6a418b1573380bb03b3b:
        Name: BG00
        MaterialInstance:
            Material: e7bf2bb6-90d8-4c1e-a8bc-78c1c46c5359:BG00
Skeleton: null`;

const SAMPLE_MATERIAL = `!MaterialAsset
Id: e7bf2bb6-90d8-4c1e-a8bc-78c1c46c5359
SerializedVersion: {Stride: 2.0.0.0}
Tags: []
Attributes:
    Diffuse: !MaterialDiffuseMapFeature
        DiffuseMap: !ComputeTextureColor
            Texture: f16ed18b-ed49-4471-ab10-bb4ee742bdbb:BG_00 Texture
            FallbackValue:
                Value: {R: 1.0, G: 1.0, B: 1.0, A: 1.0}`;

suite('Asset Parser Tests', () => {
    test('parseAssetHeader: Model', () => {
        const header = parseAssetHeader(SAMPLE_MODEL);
        assert.ok(header);
        assert.strictEqual(header!.typeName, 'Model');
        assert.strictEqual(header!.id, 'f16bfc07-e816-475d-a400-304b8042d6c7');
        assert.strictEqual(header!.sourcePath, '../../Resources/model/BG/scene/bg_00.FBX');
    });

    test('parseAssetHeader: Material (no source)', () => {
        const header = parseAssetHeader(SAMPLE_MATERIAL);
        assert.ok(header);
        assert.strictEqual(header!.typeName, 'MaterialAsset');
        assert.strictEqual(header!.id, 'e7bf2bb6-90d8-4c1e-a8bc-78c1c46c5359');
        assert.strictEqual(header!.sourcePath, undefined);
    });

    test('parseAssetHeader: invalid content', () => {
        assert.strictEqual(parseAssetHeader('not yaml'), null);
        assert.strictEqual(parseAssetHeader(''), null);
    });

    test('parseAssetFull: Model with references', () => {
        const parsed = parseAssetFull(SAMPLE_MODEL);
        assert.ok(parsed);
        assert.strictEqual(parsed!.typeName, 'Model');
        // Should find the material reference but not its own Id
        const materialRef = parsed!.references.find(r => r.name === 'BG00');
        assert.ok(materialRef, 'Should find material reference');
        assert.strictEqual(materialRef!.guid, 'e7bf2bb6-90d8-4c1e-a8bc-78c1c46c5359');
    });

    test('parseAssetFull: Material with texture reference', () => {
        const parsed = parseAssetFull(SAMPLE_MATERIAL);
        assert.ok(parsed);
        const texRef = parsed!.references.find(r => r.name === 'BG_00 Texture');
        assert.ok(texRef, 'Should find texture reference');
        assert.strictEqual(texRef!.guid, 'f16ed18b-ed49-4471-ab10-bb4ee742bdbb');
    });

    test('parseAssetFull: source paths', () => {
        const parsed = parseAssetFull(SAMPLE_MODEL);
        assert.ok(parsed);
        assert.ok(parsed!.sourcePaths.length >= 1);
        assert.strictEqual(parsed!.sourcePaths[0].path, '../../Resources/model/BG/scene/bg_00.FBX');
    });

    test('headerToAssetEntry: correct mapping', () => {
        const header = parseAssetHeader(SAMPLE_MODEL)!;
        const entry = headerToAssetEntry(header, '/some/path/bg_00.sdm3d');
        assert.strictEqual(entry.id, 'f16bfc07-e816-475d-a400-304b8042d6c7');
        assert.strictEqual(entry.location, 'bg_00');
        assert.strictEqual(entry.extension, '.sdm3d');
        assert.strictEqual(entry.typeName, 'Model');
    });
});
