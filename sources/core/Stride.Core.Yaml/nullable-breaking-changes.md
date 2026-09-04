# Nullable reference type changes — public API impact

`Stride.Core.Yaml` is a public library. These annotations are metadata-only (no IL/behavior
change, not binary-breaking) but are source-breaking for consumers who build with
`<Nullable>enable</Nullable>`: previously-silent call sites may now get new nullable warnings,
and `!`-based workarounds some consumers added may become unnecessary.

## Events/* (Cluster 1)

- `Events.Scalar` ctors: `anchor`/`tag` parameters `string` → `string?`. Both were already
  optional by design (callers commonly pass `null`); this only makes the existing contract
  explicit.
- `Events.MappingStart`, `Events.SequenceStart` ctors: same `anchor`/`tag` → `string?`.
- `Events.DocumentStart`: `Tags` property and `version`/`tags` ctor parameters → nullable
  (`TagDirectiveCollection?`, `VersionDirective?`). Optional for implicit documents.

## Tokens/* + root (Cluster 2)

- `IParser.Current`: `ParsingEvent` → `ParsingEvent?`. **User-confirmed.** Both implementations
  (`Parser`, `MemoryParser`) genuinely return `null` before the first `MoveNext()` call and once
  the stream is exhausted — the non-nullable signature was already inaccurate. Consumers that
  dereference `IParser.Current` without a null check will start seeing `CS8602`/`CS8604` under
  their own `<Nullable>enable</Nullable>` builds.
- `EventReader.Allow<T>()`, `EventReader.Peek<T>()`: return type `T` → `T?` (`T` constrained to
  `Event`, a reference type). Both already returned `null` when the current event did not match
  `T` — this was the pre-existing contract, now made explicit.

## Schemas/* (Cluster 3)

- `IYamlSchema.ExpandTag`/`ShortenTag`: parameter and return `string` → `string?`. Both already
  accepted and returned `null` explicitly (`if (x == null) return null;`).
- `IYamlSchema.GetDefaultTag(NodeEvent)`, `GetDefaultTag(Type)`: return `string` → `string?`.
  Both already returned `null` on lookup failure.
- `IYamlSchema.IsTagImplicit`: parameter `string` → `string?`. Already treated `null` as
  implicit (`if (tag == null) return true;`).
- `IYamlSchema.TryParse` (both overloads): `out string defaultTag`/`out object value` →
  `out string?`/`out object?`. Both were already initialized to `null` and could remain `null`
  even when the call succeeds (e.g. the "expand tag" fallback path).
- `IYamlSchema.GetTypeForDefaultTag`: parameter and return `Type`/`string` → nullable. XML doc
  already documented "return null if no default tag associated".

## Serialization/* small types (Cluster 4)

- `ObjectContext` (struct): `Instance` property and ctor parameter → `object?` (XML doc already
  documented "if not null, the instance..."); `ParentTypeDescriptor`/`ParentTypeMemberDescriptor`
  (+ ctor params) → nullable (already defaulted to `null`); `Tag`/`Anchor` → nullable (never set
  by the constructor, only via object-initializer).
- `EventInfo` and every derived event-info type (`AliasEventInfo`, `ObjectEventInfo`,
  `ScalarEventInfo`, `MappingStart/EndEventInfo`, `SequenceStart/EndEventInfo`): `sourceValue`
  ctor parameter and `SourceValue` property → `object?` (a serialized value can genuinely be a
  CLR `null`, e.g. a null field emitted as a YAML null scalar). `Alias`/`Anchor`/`Tag`/
  `RenderedValue` → nullable (optional, never required).
- `YamlNode.Anchor`/`Tag` → nullable (a node may have no anchor; `Tag` mirrors the already-nullable
  `NodeEvent.Tag`).
- `YamlScalarNode.Value` → `string?`; `ToString()` and the explicit `(string)` conversion operator
  → nullable return, to match (a scalar node constructed via the parameterless constructor has no
  value until set).
- `YamlScalarNode`/`YamlMappingNode`/`YamlAliasNode`/`YamlSequenceNode.Equals(object)` →
  `Equals(object?)`, matching `object.Equals`'s own nullable parameter (was a source-level
  mismatch already, just not flagged until nullable was enabled on this project).
- `DefaultObjectFactory.GetDefaultImplementation`: return `Type` → `Type?` (already returned
  `null` for a `null` input).
- `ChainedObjectFactory` ctor parameter and field `nextFactory` → `IObjectFactory?` (already
  checked for `null` in `Create`).
- `SerializerContext.Logger` → `ILogger?` (mirrors `SerializerContextSettings.Logger`, below;
  consumers already null-conditional it, e.g. `logger?.Warning(...)`).
- `SerializerContextSettings.Logger` → `ILogger?` (optional, never required).
- `SerializerSettings`: ctor parameter `schema` → `IYamlSchema?` (already coalesced to
  `new CoreSchema()`); `PreSerializer`/`PostSerializer`/`ChainedSerializerFactory` → nullable
  (optional extension points, never assigned a default); `ComparerForKeySorting` → nullable (XML
  doc already documented "this value can be set to null to disable the default comparer").
- `OrderedDictionary<TKey, TValue>` now constrains `TKey : notnull` (required by the underlying
  `KeyedCollection<TKey, TItem>`, which itself requires `notnull` — source-breaking only for a
  caller that instantiated this type with a nullable-annotated reference type or `Nullable<T>`
  key, which was already unsound).
- `DefaultObjectFactory.GetDefaultImplementation`: **correction to the Cluster 4 entry above** —
  the `type` parameter itself also needed to become `Type?` (not just the return), since the
  method's own body already null-checks it (`if (type == null) return null;`). Missed in
  Cluster 4, caught here when `TagTypeSerializer.cs` started passing a nullable `expectedType`.

## Serialization/Serializers/* (Cluster 5)

- `IYamlSerializable.ReadYaml`: return `object` → `object?`. A deserialized value can genuinely
  be CLR `null` (e.g. an explicit YAML `null` scalar) — every concrete serializer already
  produces or forwards `null` somewhere in this call chain. Narrower (non-null) overrides
  remain valid without any change (`ArraySerializer.ReadYaml`, `ObjectSerializer`'s own
  `ReadMembers`-based path never returns null itself, etc.).
- `IYamlSerializableFactory.TryCreate`: return `IYamlSerializable` → `IYamlSerializable?`. XML
  doc already said "or return null if not supported"; several already-migrated downstream
  projects (`Stride.Core.Assets`, `Stride.Core.Design`) had already annotated their overrides
  as `IYamlSerializable?`, anticipating this exact change.
- `ScalarSerializerBase.ConvertFrom`/`ReadYaml` → `object?` (`PrimitiveSerializer.ConvertFrom`
  returns `null` for a null scalar mapped to an object/string-typed member).
- `ChainedSerializer`: `Prev`/`Next` → nullable (first/last link in the chain); `Prepend`/
  `Append` parameters → nullable (both already null-checked via `?.`); `ReadYaml` → `object?`;
  `FindPrevious<T>`/`FindNext<T>`/`FindByType<T>` → `T?` (already marked `[CanBeNull]` with the
  older Stride.Core.Annotations convention — native `?` now makes it compiler-checked).
- `AnchorSerializer.TryGetAliasValue`: `out object value` → `out object?` (an anchored value can
  itself be `null`).
- `ObjectSerializer`: `ReadYaml`/`TryCreate` → nullable, matching the interfaces above.
  `TryReadMember` (both overloads): `out string memberName` → `out string?` (stays `null` if a
  deeper call throws before assigning it — already handled defensively by callers via
  `memberName ?? "(Unknown)"`). `ReadMemberValue`/`WriteMemberValue` (`protected virtual`,
  overridable by subclasses): `member`/`memberValue` parameters → nullable
  (`DictionarySerializer` already calls `ReadMemberValue` with a literal `null` member).
  `WriteMemberName` (`protected virtual`): `member` parameter → nullable (same reason).
- `IObjectSerializerBackend` (and `DefaultObjectSerializerBackend`'s implementation):
  `ReadMemberValue`/`ReadCollectionItem`/`ReadDictionaryKey`/`ReadDictionaryValue` return →
  `object?` (all four delegate to the now-nullable `ReadYaml`); their `memberValue`/`value`
  parameters → nullable (member values and collection items can be `null`); `WriteMemberValue`/
  `WriteCollectionItem`/`WriteDictionaryValue`'s value-carrying parameters → nullable to match;
  `WriteMemberName`'s `member` parameter → nullable. Dictionary/collection *keys* were left
  non-nullable throughout (`DictionaryDescriptor.AddToDictionary`'s `key` parameter, in the
  already-migrated Stride.Core.Reflection, is non-null) — `DictionarySerializer.ReadDictionaryItem`
  uses `!` on the key result instead, since dictionary keys are never null in practice.
- `DictionarySerializer.ReadDictionaryItem`/`WriteDictionaryItem` (`protected virtual`):
  `KeyValuePair<object, object>` → `KeyValuePair<object, object?>` (values can be null; keys
  stay non-null, matching `DictionaryDescriptor.GetEnumerator`'s own
  `IEnumerable<KeyValuePair<object, object?>>`).
- `RoutingSerializer.ReadYaml` → `object?` (matches the interface; routes through to whatever
  the resolved serializer's `ReadYaml` returns).
