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
  - **Cluster 2 (Tokens/\* + small root files)**: `Tokens/VersionDirective.cs`,
    `Tokens/TagDirective.cs`, `YamlException.cs`, `Version.cs`, `MemoryParser.cs`,
    `EventReader.cs`, `IParser.cs`. Fixed, verified 0 warnings in all 7 files. Not yet committed.
    - `Equals(object obj)` → `Equals(object? obj)` + `as`-cast target to nullable local
      (`VersionDirective`, `TagDirective`, `Version`).
    - `YamlException`: optional ctor params (`message`, `inner`, `innerException`) → `?`. Third
      ctor (`ParsingEvent node, Exception innerException`, no default) left non-null —
      dereferences `node` unconditionally, never defaulted.
    - **`IParser.Current` widened to `ParsingEvent?`** (user-confirmed public API change, logged
      below) — both implementations (`Parser.current` field, `MemoryParser.current` field) are
      genuinely null before the first `MoveNext()`/at end of stream. `MemoryParser.Current`
      updated to match (`ParsingEvent?`). `Parser.cs` itself left untouched (deferred to its own
      "big files" pass — a non-nullable override of a nullable interface member is valid C#, so
      no warning forces the change yet).
    - `EventReader.Allow<T>`/`Peek<T>` (`where T : Event`, so `T` is reference-type-constrained)
      → return type widened to `T?`; cast from `parser.Current` uses `!` with a comment (`Accept<T>`
      already confirmed the type/non-nullness, compiler can't see across the method boundary).
      `Expect<T>` binds `parser.Current!` to a local once (pre-existing implicit assumption:
      `EventReader` ctor calls `MoveNext()` once) instead of repeating `!` per access.
      `ReadCurrent`'s `events.Add(Allow<Event>())` → `Allow<Event>()!` (null only possible at end
      of stream, pre-existing behavior unchanged).
    - **Ripple effect (expected, not fixed here):** widening `IParser.Current` surfaces a few new
      warnings in not-yet-migrated consumers (`ObjectSerializer.cs:329`, `DictionarySerializer.cs:163`
      CS8602; `ArraySerializer.cs:145`, `DefaultObjectSerializerBackend.cs:188`, `Serializer.cs:530`
      CS8604 passing `node`/`currentEvent` into `YamlException`). All fall inside files already
      slated for the Serialization/* and Serialization/Serializers/* passes below — left alone,
      will be fixed when those files' turn comes.
  - **Cluster 3 (Schemas/\*)**: `IYamlSchema.cs`, `SchemaBase.cs`, `FailsafeSchema.cs`,
    `JsonSchema.cs`, `CoreSchema.cs`, `ExtendedSchema.cs`. Fixed, verified 0 warnings in all 6
    files. Not yet committed.
    - **Public interface `IYamlSchema` widened** (unambiguous from existing code, not asked —
      every widened member already had an explicit `if (x == null) return null;`/`TryGetValue`-into-return
      pattern in `SchemaBase`'s implementation, so the non-nullable signature was already
      inaccurate): `ExpandTag`/`ShortenTag` (param + return → `?`), `GetDefaultTag(NodeEvent)` and
      `GetDefaultTag(Type)` (return → `?`), `IsTagImplicit` (param → `?`), `TryParse` (both
      overloads: `out defaultTag`/`out value` → `?`), `GetTypeForDefaultTag` (param + return → `?`).
      `RegisterTag` unchanged (already guarded with `ArgumentNullException`, guards kept).
      `FailsafeSchema.TryParse` override updated to match.
    - `SchemaBase`'s private nested `ScalarResolutionRule`: `Decoder` return → `object?` (the
      `"!!null"` rule genuinely decodes to CLR `null` by design); `Encoder` field → `Func<object,
      string>?` (the non-generic `AddScalarRule(Type[], ...)` overload is called with a literal
      `null` encoder in three places). `Encode(object)` dereferences `Encoder!` — pre-existing
      contract is "call only after `HasEncoder()`", not enforced by the compiler; not fixed here
      (would be a behavior change, not an annotation).
    - `AddScalarRule<T>`'s `encode` param → `Func<T, string>?`; its internal wrapper lambda uses
      `encode!` — the wrapper is stored unconditionally regardless of whether `encode` was null
      (pre-existing latent bug: `Encode()` would NRE if ever called on such a rule — not fixed,
      zero-behavior-change).
    - Three `AddScalarRule<object>("!!null", ..., m => null, null)` call sites (`CoreSchema.cs`,
      `ExtendedSchema.cs`, `JsonSchema.cs`) → `AddScalarRule<object?>(...)` so the null-literal
      decode lambda type-checks without hiding the null.
    - **Ripple effect (expected, not fixed here):** widening `IYamlSchema` surfaces new warnings
      in not-yet-migrated consumers — `TagTypeSerializer.cs:102,106`, `SerializerContext.cs:222`,
      `PrimitiveSerializer.cs:99,100,154,156`, `YamlAssemblyRegistry.cs:199,207`. All fall inside
      files already slated for the Serialization/*, Serialization/Serializers/*, or
      `YamlAssemblyRegistry.cs` passes below.
  - **Cluster 4 (Serialization/\* small types)**: `ObjectContext.cs`, `EventInfo.cs`,
    `YamlMappingNode.cs`, `YamlNode.cs`, `YamlScalarNode.cs`, `YamlNodeIdentityEqualityComparer.cs`,
    `YamlAliasNode.cs`, `YamlSequenceNode.cs`, `YamlDocument.cs`, `IdentityEqualityComparer.cs`,
    `DocumentLoadingState.cs`, `SerializerContext.cs`, `SerializerContextSettings.cs`,
    `SerializerFactorySelector.cs`, `DynamicMemberDescriptorBase.cs`, `DefaultObjectFactory.cs`,
    `ChainedObjectFactory.cs`, `LambdaObjectFactory.cs`, `OrderedDictionary.cs`,
    `SerializerSettings.cs`. Fixed, verified 0 warnings in all 19 files. Not yet committed.
    - Public widenings unambiguous from existing code/doc comments — see
      `nullable-breaking-changes.md` for the full list (`ObjectContext`, `EventInfo` hierarchy,
      `YamlNode`/`YamlScalarNode`, `DefaultObjectFactory.GetDefaultImplementation`,
      `ChainedObjectFactory`, `SerializerContext.Logger`, `SerializerContextSettings.Logger`,
      `SerializerSettings` ctor param + 4 properties, `OrderedDictionary<TKey,TValue>` now
      `where TKey : notnull`).
    - `YamlScalarNode`/`YamlMappingNode`/`YamlAliasNode`/`YamlSequenceNode.Equals(object)` →
      `Equals([NotNullWhen(true)] object?)`, matching `object.Equals`'s nullable parameter.
    - `SerializerContext.Reader`/`Writer`/`Emitter`: **not** widened to nullable despite being
      genuinely two-phase (only one of Reader vs Writer+Emitter is set, depending on
      serialize/deserialize direction) — these 3 properties are read unguarded from dozens of
      call sites across the entire (mostly not-yet-migrated) Serializers layer; widening would
      have injected a CS8602 at every one of them, a ripple wildly disproportionate to this
      cluster. Used `= null!` on each instead (skill-sanctioned "always set after construction,
      by a mode the compiler can't see" pattern) — kept scope contained to this cluster's files.
    - `SerializerSettings`: `objectFactory`/`specialCollectionMember`/`objectSerializerBackend`/
      `_namingConvention` fields are only ever assigned through their own validating property
      setter (never directly), so the compiler can't see them as initialized by the constructor.
      Tried `[MemberNotNull]` on the constructor first — **doesn't compile**: the attribute is
      only valid on method/property/indexer, not constructors (confirmed by build error CS0592).
      Used `= null!` on the 4 field declarations instead.
    - `OrderedDictionary<TKey, TValue>.TryGetValue`: `out TValue value` →
      `[MaybeNullWhen(false)] out TValue value` (matches `Dictionary<TKey,TValue>`'s own real
      annotation) rather than `TValue?`, since `TValue` is unconstrained and `?` on it would
      change `Nullable<T>` layout for a value-type instantiation.
    - `ChainedObjectFactory.Create`: returns a bare `null` when there is no next factory, which
      **violates `IObjectFactory.Create`'s own doc contract** ("Throws... if the type cannot be
      created"). Left as-is with `null!` + a `// TODO` comment rather than fixed — fixing it
      (throw instead of returning null) would be a behavior change, out of scope for annotation.
    - **Ripple effect (expected, not fixed here; larger than prior clusters' — this cluster's
      types are used pervasively by the serializer layer coming next):** 27 new warnings across
      `AnchorEventEmitter.cs`, `ArraySerializer.cs`, `CollectionSerializer.cs` (x6),
      `DefaultObjectSerializerBackend.cs` (x3), `DictionarySerializer.cs` (x6), `ObjectSerializer.cs`
      (x5), `ScalarSerializerBase.cs`, `Serializer.cs`, `TagTypeSerializer.cs`,
      `WriterEventEmitter.cs` (x2) — all already slated for Cluster 5 or the big-files pass.
      (Also fixed 8 pre-existing warnings in those same deferred files as a side effect of
      widening `ObjectContext.Instance`/`IYamlSchema`/etc. — net warning count still dropped.)
  - **Cluster 5 (Serialization/Serializers/\* + 2 root emitters)**: `IYamlSerializable.cs`,
    `IYamlSerializableFactory.cs`, `IObjectSerializerBackend.cs`, `ScalarSerializerBase.cs`,
    `ExceptionUtils.cs`, `ChainedSerializer.cs`, `AnchorSerializer.cs`, `ArraySerializer.cs`,
    `PrimitiveSerializer.cs`, `ObjectSerializer.cs`, `CollectionSerializer.cs`,
    `DictionarySerializer.cs`, `TagTypeSerializer.cs`, `DefaultObjectSerializerBackend.cs`,
    `RoutingSerializer.cs`, `AnchorEventEmitter.cs`, `WriterEventEmitter.cs`. Plus a correction
    to `DefaultObjectFactory.GetDefaultImplementation`'s parameter (missed in Cluster 4). Fixed,
    verified 0 warnings in all 18 files. Not yet committed.
    - **Two core interfaces widened** (both unambiguous — see `nullable-breaking-changes.md`):
      `IYamlSerializable.ReadYaml` → `object?` (a deserialized value can genuinely be CLR
      `null`); `IYamlSerializableFactory.TryCreate` → `IYamlSerializable?` (doc already said "or
      null"; downstream `Stride.Core.Assets`/`Stride.Core.Design` — already nullable-enabled,
      independently of this branch — had *already* annotated their overrides as
      `IYamlSerializable?` in anticipation, confirming the direction was expected). Every
      implementer across this cluster updated to match (narrower non-null overrides where a
      serializer provably never returns null, e.g. `ArraySerializer.ReadYaml`, needed no change —
      safe/covariant direction).
    - **`IObjectSerializerBackend` widened to match its `DefaultObjectSerializerBackend`
      implementation**, which just forwards to the now-nullable `ReadYaml`:
      `ReadMemberValue`/`ReadCollectionItem`/`ReadDictionaryKey`/`ReadDictionaryValue` return
      `object?`; their value-carrying parameters nullable to match (member values/collection
      items can be `null`). Dictionary *keys* stay non-null throughout (matches
      `DictionaryDescriptor.AddToDictionary`'s already-migrated `object key` in
      Stride.Core.Reflection) — `DictionarySerializer.ReadDictionaryItem` uses `!` on the key
      result instead of threading nullability through, since keys are never null in practice.
    - `ChainedSerializer.Prev`/`Next` widened to nullable; `FindBoundary`'s inner loop rewritten
      to cache the `navigate(current)` call in a local instead of invoking it twice per
      iteration (same number of *distinct* results, same behavior — needed because the compiler
      doesn't narrow across repeated method-call expressions the way it does for locals).
    - `ObjectSerializer.TryReadMember`: removed a dead `memberScalar = null;` initializer instead
      of widening `Scalar` to nullable — it was unconditionally overwritten 6 lines later before
      any use, so `Scalar` genuinely never ends up null; only `memberName` (which stays at its
      `null` default if a deeper call throws first) needed widening.
    - **Known pre-existing tension not fixed (flagged only):**
      `DefaultObjectSerializerBackend.ShouldSerialize` passes `objectContext.ParentTypeMemberDescriptor`
      (genuinely `null` for a root, parent-less object — not just a theoretical case) into
      `ShouldSerializePredicate`'s non-nullable parameter (fixed, in the separately-migrated
      Stride.Core.Reflection project). Used `!` to compile; this doesn't change the underlying
      tension, just makes the type system stop hiding it.
    - **Ripple effect (expected, small):** one new warning, `Serializer.cs(521,30)` CS8600 —
      already slated for the big-files-last pass. Net distinct-warning count across the whole
      project dropped from 223 to 151.

## Not started (~150 warnings, ~17 files)

Planned order (small/foundational → large):

1. ~~Finish Cluster 2 above.~~ Done.
2. ~~**Schemas/\***~~ Done.
3. ~~**Serialization/\* small types**~~ Done.
4. ~~**Serialization/Serializers/\***~~ Done.
5. `YamlAssemblyRegistry.cs` remaining enable-wave warnings (separate from the wave-1
   dereference fixes already committed).
6. **Big files last**: `SortedDictionary.cs` (separate from wave-1 fixes already committed),
   `Parser.cs`, `Serializer.cs`, `Emitter.cs`, `Scanner.cs`.
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
