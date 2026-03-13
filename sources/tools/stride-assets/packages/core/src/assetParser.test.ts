import { describe, it, expect } from 'vitest';
import { parseAssetHeader, parseAssetFull, headerToAssetEntry } from './assetParser';

describe('parseAssetHeader', () => {
    it('parses a simple asset header', () => {
        const content = [
            '!MaterialAsset',
            'Id: f16ed18b-ed49-4471-ab10-bb4ee742bdbb',
            'Source: ../../Resources/texture.png',
        ].join('\n');
        const header = parseAssetHeader(content);
        expect(header).not.toBeNull();
        expect(header!.typeName).toBe('MaterialAsset');
        expect(header!.id).toBe('f16ed18b-ed49-4471-ab10-bb4ee742bdbb');
        expect(header!.sourcePath).toBe('../../Resources/texture.png');
    });

    it('lowercases the GUID', () => {
        const content = [
            '!Texture',
            'Id: AABBCCDD-1122-3344-5566-778899AABBCC',
        ].join('\n');
        const header = parseAssetHeader(content);
        expect(header!.id).toBe('aabbccdd-1122-3344-5566-778899aabbcc');
    });

    it('returns null for invalid content', () => {
        expect(parseAssetHeader('')).toBeNull();
        expect(parseAssetHeader('just text')).toBeNull();
        expect(parseAssetHeader('!Type\nno id here')).toBeNull();
    });

    it('returns null when fewer than 2 lines', () => {
        expect(parseAssetHeader('!Type')).toBeNull();
    });

    it('handles missing Source line', () => {
        const content = [
            '!SceneAsset',
            'Id: 00000000-0000-0000-0000-000000000000',
            'SomeOtherField: value',
        ].join('\n');
        const header = parseAssetHeader(content);
        expect(header).not.toBeNull();
        expect(header!.sourcePath).toBeUndefined();
    });

    it('skips Source: !SystemFontProvider', () => {
        const content = [
            '!SpriteFont',
            'Id: 00000000-0000-0000-0000-000000000000',
            'Source: !SystemFontProvider {}',
        ].join('\n');
        const header = parseAssetHeader(content);
        expect(header!.sourcePath).toBeUndefined();
    });

    it('skips Source: null', () => {
        const content = [
            '!Texture',
            'Id: 00000000-0000-0000-0000-000000000000',
            'Source: null',
        ].join('\n');
        const header = parseAssetHeader(content);
        expect(header!.sourcePath).toBeUndefined();
    });
});

describe('headerToAssetEntry', () => {
    it('builds an AssetEntry from header and file path', () => {
        const header = {
            id: 'f16ed18b-ed49-4471-ab10-bb4ee742bdbb',
            typeName: 'MaterialAsset',
            sourcePath: '../../Resources/texture.png',
        };
        const entry = headerToAssetEntry(header, '/project/Assets/MyMaterial.sdmat');
        expect(entry.id).toBe('f16ed18b-ed49-4471-ab10-bb4ee742bdbb');
        expect(entry.location).toBe('MyMaterial');
        expect(entry.extension).toBe('.sdmat');
        expect(entry.typeName).toBe('MaterialAsset');
        expect(entry.sourcePath).toBe('../../Resources/texture.png');
    });

    it('handles Windows-style paths', () => {
        const header = { id: 'aabbccdd-1122-3344-5566-778899aabbcc', typeName: 'Texture' };
        const entry = headerToAssetEntry(header, 'C:\\Projects\\Assets\\bg_wall.sdtex');
        expect(entry.location).toBe('bg_wall');
        expect(entry.extension).toBe('.sdtex');
    });
});

describe('parseAssetFull', () => {
    it('parses a complete asset with references and parts', () => {
        const content = [
            '!SceneAsset',
            'Id: 00000000-0000-0000-0000-000000000001',
            'Hierarchy:',
            '    Parts:',
            '        - Entity:',
            '            Id: aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
            '            Name: Player',
            '            Components:',
            '                abc123: !TransformComponent',
            '                    Position: {X: 0, Y: 0, Z: 0}',
            '        - Entity:',
            '            Id: 11111111-2222-3333-4444-555555555555',
            '            Name: Camera',
            '    RootParts:',
            '        - ref!! aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
        ].join('\n');
        const parsed = parseAssetFull(content);
        expect(parsed).not.toBeNull();
        expect(parsed!.id).toBe('00000000-0000-0000-0000-000000000001');
        expect(parsed!.typeName).toBe('SceneAsset');
        expect(parsed!.parts).toHaveLength(2);
        expect(parsed!.parts[0].id).toBe('aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee');
        expect(parsed!.parts[0].name).toBe('Player');
        expect(parsed!.parts[1].id).toBe('11111111-2222-3333-4444-555555555555');
        expect(parsed!.parts[1].name).toBe('Camera');
    });

    it('filters out the asset own Id from references', () => {
        const content = [
            '!PrefabAsset',
            'Id: 00000000-0000-0000-0000-000000000001',
            '    ref: 00000000-0000-0000-0000-000000000001:self',
            '    ref: 11111111-2222-3333-4444-555555555555:other',
        ].join('\n');
        const parsed = parseAssetFull(content);
        // Should only have 1 reference (not self-reference)
        expect(parsed!.references).toHaveLength(1);
        expect(parsed!.references[0].guid).toBe('11111111-2222-3333-4444-555555555555');
    });

    it('handles Name*: archetype override syntax', () => {
        const content = [
            '!SceneAsset',
            'Id: 00000000-0000-0000-0000-000000000001',
            '    Parts:',
            '        - Entity:',
            '            Id: aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
            '            Name*: OverriddenPlayer',
        ].join('\n');
        const parsed = parseAssetFull(content);
        expect(parsed!.parts).toHaveLength(1);
        expect(parsed!.parts[0].name).toBe('OverriddenPlayer');
    });

    it('returns null for invalid content', () => {
        expect(parseAssetFull('not valid')).toBeNull();
    });
});
