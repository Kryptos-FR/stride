// Core types
export { AssetEntry, AssetPartEntry, BackReference } from './types';

// Reference pattern matching
export {
    AssetReferenceMatch,
    PartReferenceMatch,
    SourcePathMatch,
    ScriptReferenceMatch,
    PropertyKeyMatch,
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

// Asset parsing
export {
    AssetHeader,
    ParsedAsset,
    AssetPartDefinition,
    parseAssetHeader,
    parseAssetFull,
    headerToAssetEntry,
} from './assetParser';

// Package parsing
export {
    PackageInfo,
    parsePackageFile,
} from './packageParser';
