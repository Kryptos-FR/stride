import { describe, it, expect } from 'vitest';
import {
    findAssetReferences,
    findPartReferences,
    findSourcePaths,
    findScriptReferences,
    getAssetReferenceAtPosition,
    getPartReferenceAtPosition,
    getSourcePathAtPosition,
    getScriptReferenceAtPosition,
    getPropertyKeyAtPosition,
    findContainingScriptType,
    isGuid,
} from './referencePattern';

describe('isGuid', () => {
    it('accepts valid GUIDs', () => {
        expect(isGuid('f16ed18b-ed49-4471-ab10-bb4ee742bdbb')).toBe(true);
        expect(isGuid('00000000-0000-0000-0000-000000000000')).toBe(true);
        expect(isGuid('ABCDEF01-2345-6789-ABCD-EF0123456789')).toBe(true);
    });

    it('rejects invalid GUIDs', () => {
        expect(isGuid('not-a-guid')).toBe(false);
        expect(isGuid('f16ed18b-ed49-4471-ab10')).toBe(false);
        expect(isGuid('')).toBe(false);
        expect(isGuid('f16ed18b-ed49-4471-ab10-bb4ee742bdbb-extra')).toBe(false);
    });
});

describe('findAssetReferences', () => {
    it('finds GUID:Name references', () => {
        const text = `    Material: f16ed18b-ed49-4471-ab10-bb4ee742bdbb:BG_00 Texture`;
        const refs = findAssetReferences(text);
        expect(refs).toHaveLength(1);
        expect(refs[0].guid).toBe('f16ed18b-ed49-4471-ab10-bb4ee742bdbb');
        expect(refs[0].name).toBe('BG_00 Texture');
    });

    it('finds multiple references on different lines', () => {
        const text = [
            '    Model: 82360e61-196c-4c07-9fae-af7ee4f265a1:bg_b00',
            '    Texture: ed43165a-c85c-4360-bfcf-d98f099875d6:BG_wall01',
        ].join('\n');
        const refs = findAssetReferences(text);
        expect(refs).toHaveLength(2);
        expect(refs[0].line).toBe(0);
        expect(refs[0].name).toBe('bg_b00');
        expect(refs[1].line).toBe(1);
        expect(refs[1].name).toBe('BG_wall01');
    });

    it('handles names with forward slashes (paths)', () => {
        const text = `    ref: 48c406d2-677a-4a3b-90a0-4c0094894935:VFXPrefabs/vfx-GetCoin`;
        const refs = findAssetReferences(text);
        expect(refs).toHaveLength(1);
        expect(refs[0].name).toBe('VFXPrefabs/vfx-GetCoin');
    });

    it('stops name at }, ], or comma', () => {
        const text = `    ref: f16ed18b-ed49-4471-ab10-bb4ee742bdbb:MyAsset}`;
        const refs = findAssetReferences(text);
        expect(refs).toHaveLength(1);
        expect(refs[0].name).toBe('MyAsset');
    });

    it('returns correct column positions', () => {
        const text = `    Model: 82360e61-196c-4c07-9fae-af7ee4f265a1:bg_b00`;
        const refs = findAssetReferences(text);
        expect(refs[0].startColumn).toBe(11); // after "    Model: "
        expect(refs[0].endColumn).toBe(11 + 36 + 1 + 6); // GUID + : + name
    });

    it('lowercases GUIDs', () => {
        const text = `    ref: F16ED18B-ED49-4471-AB10-BB4EE742BDBB:MyAsset`;
        const refs = findAssetReferences(text);
        expect(refs[0].guid).toBe('f16ed18b-ed49-4471-ab10-bb4ee742bdbb');
    });
});

describe('findPartReferences', () => {
    it('finds ref!! GUID references', () => {
        const text = `        Entity: ref!! d6b5de04-5127-4e41-84d3-a79e150a7f8e`;
        const refs = findPartReferences(text);
        expect(refs).toHaveLength(1);
        expect(refs[0].guid).toBe('d6b5de04-5127-4e41-84d3-a79e150a7f8e');
    });

    it('finds multiple part references', () => {
        const text = [
            '    - ref!! aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
            '    - ref!! 11111111-2222-3333-4444-555555555555',
        ].join('\n');
        const refs = findPartReferences(text);
        expect(refs).toHaveLength(2);
        expect(refs[0].line).toBe(0);
        expect(refs[1].line).toBe(1);
    });

    it('lowercases GUIDs', () => {
        const text = `    ref!! AABBCCDD-1122-3344-5566-778899AABBCC`;
        const refs = findPartReferences(text);
        expect(refs[0].guid).toBe('aabbccdd-1122-3344-5566-778899aabbcc');
    });
});

describe('findSourcePaths', () => {
    it('finds Source: lines', () => {
        const text = `Source: ../../Resources/model/BG/sourceimages/BG_wall01.jpg`;
        const refs = findSourcePaths(text);
        expect(refs).toHaveLength(1);
        expect(refs[0].path).toBe('../../Resources/model/BG/sourceimages/BG_wall01.jpg');
    });

    it('skips !dir and !SystemFontProvider tags', () => {
        const text = [
            'Source: !SystemFontProvider {}',
            'Source: ../../actual/path.png',
        ].join('\n');
        const refs = findSourcePaths(text);
        expect(refs).toHaveLength(1);
        expect(refs[0].path).toBe('../../actual/path.png');
    });

    it('skips null sources', () => {
        const text = `Source: null`;
        const refs = findSourcePaths(text);
        expect(refs).toHaveLength(0);
    });

    it('handles indented Source lines', () => {
        const text = `    Source: ../textures/foo.png`;
        const refs = findSourcePaths(text);
        expect(refs).toHaveLength(1);
        expect(refs[0].path).toBe('../textures/foo.png');
    });

    it('trims trailing whitespace and \\r', () => {
        const text = "Source: ../path/file.png\r";
        const refs = findSourcePaths(text);
        expect(refs).toHaveLength(1);
        expect(refs[0].path).toBe('../path/file.png');
    });
});

describe('findScriptReferences', () => {
    it('finds !Namespace.Type,Assembly references', () => {
        const text = `            960e8f78680f474b822fa746b2ea27a9: !SpaceEscape.Background.ObstacleInfo,SpaceEscape.Game`;
        const refs = findScriptReferences(text);
        expect(refs).toHaveLength(1);
        expect(refs[0].typeName).toBe('SpaceEscape.Background.ObstacleInfo');
        expect(refs[0].assemblyName).toBe('SpaceEscape.Game');
    });

    it('requires at least one dot in the type name', () => {
        // Single word !tags are YAML type markers, not scripts
        const text = `    !TransformComponent`;
        const refs = findScriptReferences(text);
        expect(refs).toHaveLength(0);
    });

    it('handles types with multiple namespace levels', () => {
        const text = `    comp: !A.B.C.D,My.Assembly.Name`;
        const refs = findScriptReferences(text);
        expect(refs).toHaveLength(1);
        expect(refs[0].typeName).toBe('A.B.C.D');
        expect(refs[0].assemblyName).toBe('My.Assembly.Name');
    });
});

describe('getAssetReferenceAtPosition', () => {
    const text = `    Model: 82360e61-196c-4c07-9fae-af7ee4f265a1:bg_b00`;

    it('returns match when cursor is on the reference', () => {
        const result = getAssetReferenceAtPosition(text, 0, 20);
        expect(result).toBeDefined();
        expect(result!.guid).toBe('82360e61-196c-4c07-9fae-af7ee4f265a1');
    });

    it('returns undefined when cursor is before the reference', () => {
        const result = getAssetReferenceAtPosition(text, 0, 5);
        expect(result).toBeUndefined();
    });

    it('returns undefined for wrong line', () => {
        const result = getAssetReferenceAtPosition(text, 1, 20);
        expect(result).toBeUndefined();
    });
});

describe('getPartReferenceAtPosition', () => {
    const text = `    - ref!! d6b5de04-5127-4e41-84d3-a79e150a7f8e`;

    it('returns match when cursor is on the ref', () => {
        const result = getPartReferenceAtPosition(text, 0, 15);
        expect(result).toBeDefined();
        expect(result!.guid).toBe('d6b5de04-5127-4e41-84d3-a79e150a7f8e');
    });

    it('returns undefined when cursor is elsewhere', () => {
        const result = getPartReferenceAtPosition(text, 0, 2);
        expect(result).toBeUndefined();
    });
});

describe('getSourcePathAtPosition', () => {
    const text = `Source: ../../Resources/model/BG/file.jpg`;

    it('returns match when cursor is on the path', () => {
        const result = getSourcePathAtPosition(text, 0, 15);
        expect(result).toBeDefined();
        expect(result!.path).toBe('../../Resources/model/BG/file.jpg');
    });

    it('returns undefined when cursor is on the key', () => {
        const result = getSourcePathAtPosition(text, 0, 2);
        expect(result).toBeUndefined();
    });
});

describe('getScriptReferenceAtPosition', () => {
    const text = `    comp: !SpaceEscape.GameScript,SpaceEscape.Game`;

    it('returns match when cursor is on the type', () => {
        const result = getScriptReferenceAtPosition(text, 0, 15);
        expect(result).toBeDefined();
        expect(result!.typeName).toBe('SpaceEscape.GameScript');
    });

    it('returns undefined when cursor is elsewhere', () => {
        const result = getScriptReferenceAtPosition(text, 0, 2);
        expect(result).toBeUndefined();
    });
});

describe('getPropertyKeyAtPosition', () => {
    it('returns the property key when cursor is on it', () => {
        const text = [
            '!SceneAsset',
            'Id: 00000000-0000-0000-0000-000000000000',
            '    MaxNbObstacles: 1',
        ].join('\n');
        const result = getPropertyKeyAtPosition(text, 2, 6);
        expect(result).toBeDefined();
        expect(result!.key).toBe('MaxNbObstacles');
    });

    it('skips structural keys like Id, Name, Components', () => {
        const text = '    Id: some-value\n    Name: foo\n    Components:\n';
        expect(getPropertyKeyAtPosition(text, 0, 5)).toBeUndefined();
        expect(getPropertyKeyAtPosition(text, 1, 5)).toBeUndefined();
        expect(getPropertyKeyAtPosition(text, 2, 5)).toBeUndefined();
    });

    it('returns undefined when cursor is on the value', () => {
        const text = '    Speed: 5.0';
        const result = getPropertyKeyAtPosition(text, 0, 12);
        expect(result).toBeUndefined();
    });

    it('handles archetype override syntax (Name*:)', () => {
        // The PROPERTY_KEY_REGEX supports optional * before the colon
        const text = '    Speed*: 5.0';
        const result = getPropertyKeyAtPosition(text, 0, 6);
        expect(result).toBeDefined();
        expect(result!.key).toBe('Speed');
    });
});

describe('findContainingScriptType', () => {
    it('walks back to find the containing !Type,Assembly', () => {
        const text = [
            '    Components:',
            '        abc123: !SpaceEscape.Background.BackgroundInfo,SpaceEscape.Game',
            '            MaxNbObstacles: 1',
            '            Holes: {}',
        ].join('\n');
        const result = findContainingScriptType(text, 2);
        expect(result).toBeDefined();
        expect(result!.typeName).toBe('SpaceEscape.Background.BackgroundInfo');
        expect(result!.assemblyName).toBe('SpaceEscape.Game');
    });

    it('returns undefined for framework types (no dot in name)', () => {
        const text = [
            '    Components:',
            '        abc123: !TransformComponent',
            '            Position: {X: 0, Y: 0, Z: 0}',
        ].join('\n');
        const result = findContainingScriptType(text, 2);
        expect(result).toBeUndefined();
    });

    it('stops at the nearest parent with lower indentation', () => {
        const text = [
            '        outer: !SpaceEscape.Outer,SpaceEscape.Game',
            '            InnerProp: 1',
            '            Nested:',
            '                inner: !SpaceEscape.Inner,SpaceEscape.Game',
            '                    DeepProp: 2',
        ].join('\n');
        const result = findContainingScriptType(text, 4);
        expect(result).toBeDefined();
        expect(result!.typeName).toBe('SpaceEscape.Inner');
    });

    it('returns undefined when no script type is found', () => {
        const text = [
            'SomeRootKey:',
            '    Property: value',
        ].join('\n');
        const result = findContainingScriptType(text, 1);
        expect(result).toBeUndefined();
    });
});
