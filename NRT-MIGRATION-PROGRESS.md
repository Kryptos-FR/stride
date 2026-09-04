# Nullable reference migration progress (temporary, will be dropped)

Branch: `feature/core-nullable-annotations`. Skill: `dotnet-upgrade:migrate-nullable-references`.

## Done

- **Stride.Core.Tasks**: `<Nullable>enable</Nullable>`, 0 warnings. Commit `f13ec85c9`.
  - Also touched `sources/assets/Stride.AssetCompiler/Tasks/PackAssets.cs` (shared/linked file):
    scoped via file-level `#nullable enable` since its home project (`Stride.AssetCompiler`)
    stays oblivious for now (too many oblivious engine deps to migrate today — user call).
- **Stride.Core.Yaml**: Strategy B (warnings-first, then enable), user-confirmed given 122 files.
  - Wave 1 (`<Nullable>warnings</Nullable>`, dereference/argument warnings): fixed 11 warnings
    across `AnchorSerializer.cs`, `ArraySerializer.cs`, `TagTypeSerializer.cs`,
    `SortedDictionary.cs` (x3), `YamlAssemblyRegistry.cs` (x3). Commit `210ec67e0`.
    Remaining SYSLIB0013 (Uri.EscapeUriString obsolete) warnings in `YamlAssemblyRegistry.cs`
    are pre-existing and unrelated to nullable — left alone.
  - Flipped to `<Nullable>enable</Nullable>` → 349 distinct annotation warnings across 53 files.
  - **Cluster 1 (Events/\*)**: `DocumentStart.cs`, `MappingStart.cs`, `NodeEvent.cs`, `Scalar.cs`,
    `SequenceStart.cs`. Fixed, verified 0 warnings. Commit `17b58f1cf`.
    Pattern: `anchor`/`tag` are genuinely optional (ctors null-check for emptiness, not nullness);
    `DocumentStart.version`/`tags` optional for implicit documents.

## In progress — Cluster 2 (Tokens/\* + small root files), NOT yet edited or committed

Analyzed, fixes identified but not applied:

- `Tokens/VersionDirective.cs` (2 warnings): `Equals(object obj)` → `Equals(object? obj)`
  (CS8765); `VersionDirective other = obj as VersionDirective;` → `VersionDirective? other = ...`
  (CS8600).
- `Tokens/TagDirective.cs` (2 warnings): same two patterns (`Equals(object?)`, `as`-cast to
  nullable local).
- `YamlException.cs` (3 warnings): ctor defaults `string message = null, Exception inner = null`
  → `string? message = null, Exception? inner = null`; second ctor's
  `Exception innerException = null` → `Exception? innerException = null`. Third ctor
  (`ParsingEvent node, Exception innerException`, no default) left non-null — never defaulted.
- `Version.cs` (2 warnings): same `Equals(object?)` + `as`-cast-to-nullable-local pattern.
- `MemoryParser.cs` (2 warnings): `current` field is genuinely nullable (`Position` setter
  assigns `null` explicitly at line 33) → **open design question**: `IParser.Current` interface
  property (`sources/core/Stride.Core.Yaml/IParser.cs:58`) is currently declared
  `ParsingEvent Current { get; }` (non-nullable). Widening `MemoryParser.Current` to
  `ParsingEvent?` without widening the interface causes CS8767 (implementation nullability
  mismatch). Likely the interface itself needs `ParsingEvent? Current { get; }` — check how
  `Parser.cs`, `Scanner.cs`-derived parsers, and `EventReader.cs` consume `Current` before
  deciding (two-phase-init pattern: null before first `MoveNext()`).
- `EventReader.cs` (2 warnings): not yet mapped to specific lines/fixes. `Allow<T>`/`Peek<T>`
  (`T : Event`, unconstrained-ish generic) `return null;` when not accepted — per skill guidance
  this wants `[return: MaybeNull] T` rather than `T?` (T is constrained to a reference type here
  via `where T : Event`, so `T?` may actually be fine too — decide when fixing).

## Not started (~330 warnings, ~45 files)

Planned order (small/foundational → large):

1. Finish Cluster 2 above.
2. **Schemas/\*** (~50 warnings): `SchemaBase.cs` (14), `ExtendedSchema.cs` (15), `CoreSchema.cs`
   (11), `JsonSchema.cs` (9), `FailsafeSchema.cs` (1).
3. **Serialization/\* small types** (~57 warnings): `ObjectContext.cs`, `EventInfo.cs`,
   `YamlMappingNode.cs`, `YamlNode.cs`, `YamlScalarNode.cs`, `YamlNodeIdentityEqualityComparer.cs`,
   `YamlAliasNode.cs`, `YamlSequenceNode.cs`, `YamlDocument.cs`, `IdentityEqualityComparer.cs`,
   `DocumentLoadingState.cs`, `SerializerContext.cs`, `SerializerContextSettings.cs`,
   `SerializerFactorySelector.cs`, `DynamicMemberDescriptorBase.cs`, `DefaultObjectFactory.cs`,
   `ChainedObjectFactory.cs`, `LambdaObjectFactory.cs`, `OrderedDictionary.cs`,
   `SerializerSettings.cs`.
4. **Serialization/Serializers/\*** (~43 warnings, `AnchorSerializer.cs`/`ArraySerializer.cs`
   partly done in wave 1 but still have enable-wave warnings too): `ChainedSerializer.cs` (11),
   `DictionarySerializer.cs` (5), `TagTypeSerializer.cs` (5), `ObjectSerializer.cs` (4),
   `CollectionSerializer.cs` (3), `PrimitiveSerializer.cs` (3),
   `DefaultObjectSerializerBackend.cs` (2), `AnchorEventEmitter.cs` (1), `ExceptionUtils.cs` (1).
5. `YamlAssemblyRegistry.cs` remaining enable-wave warnings (11 — separate from the wave-1
   dereference fixes already committed).
6. **Big files last**: `SortedDictionary.cs` (69 — separate from wave-1 fixes already committed),
   `Parser.cs` (42), `Serializer.cs` (33), `Emitter.cs` (10), `Scanner.cs` (6).
7. Remaining scattered small files not yet clustered above (see full breakdown by re-running the
   command below).

## How to regenerate the exact remaining warning list

```
dotnet build "sources/core/Stride.Core.Yaml/Stride.Core.Yaml.csproj" --no-incremental -v:q 2>&1 \
  | grep "Stride\.Core\.Yaml\.csproj\]"
```

(Output is duplicated per TFM — dedupe by `(line,col): warning CODE` text before counting.)

## Key decisions made this session (don't re-litigate)

- Strategy B for Stride.Core.Yaml (user-confirmed), sequential-by-cluster execution
  (not parallel subagents), one commit per cluster.
- `Stride.AssetCompiler.csproj` itself stays nullable-oblivious for now (too many oblivious
  engine deps); only the shared `PackAssets.cs` file got a file-scoped `#nullable enable`.
- No `<WarningsAsErrors>nullable</WarningsAsErrors>` added anywhere — not the convention used by
  other already-migrated core projects on this branch (checked `Stride.Core.IO.csproj`).
