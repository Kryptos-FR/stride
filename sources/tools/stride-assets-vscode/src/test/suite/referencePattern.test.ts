import * as assert from 'assert';
import { findAssetReferences, findEntityReferences, findSourcePaths, isGuid, getAssetReferenceAtPosition } from '../../core/referencePattern';

suite('Reference Pattern Tests', () => {
    test('findAssetReferences: simple GUID:Name', () => {
        const text = 'Material: e7bf2bb6-90d8-4c1e-a8bc-78c1c46c5359:BG00';
        const refs = findAssetReferences(text);
        assert.strictEqual(refs.length, 1);
        assert.strictEqual(refs[0].guid, 'e7bf2bb6-90d8-4c1e-a8bc-78c1c46c5359');
        assert.strictEqual(refs[0].name, 'BG00');
        assert.strictEqual(refs[0].line, 0);
    });

    test('findAssetReferences: name with spaces', () => {
        const text = 'Texture: f16ed18b-ed49-4471-ab10-bb4ee742bdbb:BG_00 Texture';
        const refs = findAssetReferences(text);
        assert.strictEqual(refs.length, 1);
        assert.strictEqual(refs[0].name, 'BG_00 Texture');
    });

    test('findAssetReferences: name with slashes', () => {
        const text = 'CoinGetEffect: 48c406d2-677a-4a3b-90a0-4c0094894935:VFXPrefabs/vfx-GetCoin';
        const refs = findAssetReferences(text);
        assert.strictEqual(refs.length, 1);
        assert.strictEqual(refs[0].name, 'VFXPrefabs/vfx-GetCoin');
    });

    test('findAssetReferences: multiple on different lines', () => {
        const text = [
            '!Model',
            'Id: f16bfc07-e816-475d-a400-304b8042d6c7',
            'Materials:',
            '    Material: e7bf2bb6-90d8-4c1e-a8bc-78c1c46c5359:BG00',
            'Skeleton: abc12345-1234-1234-1234-123456789abc:MySkeleton',
        ].join('\n');
        const refs = findAssetReferences(text);
        // Should find the asset Id on line 1 plus the two references
        assert.ok(refs.length >= 2);
        const materialRef = refs.find(r => r.name === 'BG00');
        assert.ok(materialRef);
        assert.strictEqual(materialRef!.line, 3);
    });

    test('findEntityReferences: ref!! GUID', () => {
        const text = '    Children:\n        - ref!! 6ec17338-026d-4ad3-9615-4975281cf3ba';
        const refs = findEntityReferences(text);
        assert.strictEqual(refs.length, 1);
        assert.strictEqual(refs[0].guid, '6ec17338-026d-4ad3-9615-4975281cf3ba');
        assert.strictEqual(refs[0].line, 1);
    });

    test('findSourcePaths: relative path', () => {
        const text = 'Source: ../../Resources/model/BG/scene/bg_00.FBX';
        const paths = findSourcePaths(text);
        assert.strictEqual(paths.length, 1);
        assert.strictEqual(paths[0].path, '../../Resources/model/BG/scene/bg_00.FBX');
    });

    test('findSourcePaths: skips YAML type tags', () => {
        const text = 'FontSource: !SystemFontProvider';
        const paths = findSourcePaths(text);
        // Source regex looks for "Source:" prefix, "FontSource" won't match
        // But let's verify null/type-tag sources are skipped
        const text2 = 'Source: !dir ../Assets';
        const paths2 = findSourcePaths(text2);
        assert.strictEqual(paths2.length, 0);
    });

    test('findSourcePaths: skips null', () => {
        const text = 'Source: null';
        const paths = findSourcePaths(text);
        assert.strictEqual(paths.length, 0);
    });

    test('isGuid: valid GUID', () => {
        assert.ok(isGuid('e7bf2bb6-90d8-4c1e-a8bc-78c1c46c5359'));
    });

    test('isGuid: invalid', () => {
        assert.ok(!isGuid('not-a-guid'));
        assert.ok(!isGuid('e7bf2bb6-90d8-4c1e-a8bc'));
    });

    test('getAssetReferenceAtPosition: cursor on reference', () => {
        const text = '    Material: e7bf2bb6-90d8-4c1e-a8bc-78c1c46c5359:BG00';
        const ref = getAssetReferenceAtPosition(text, 0, 20);
        assert.ok(ref);
        assert.strictEqual(ref!.guid, 'e7bf2bb6-90d8-4c1e-a8bc-78c1c46c5359');
    });

    test('getAssetReferenceAtPosition: cursor outside reference', () => {
        const text = '    Material: e7bf2bb6-90d8-4c1e-a8bc-78c1c46c5359:BG00';
        const ref = getAssetReferenceAtPosition(text, 0, 0);
        assert.ok(!ref);
    });
});
