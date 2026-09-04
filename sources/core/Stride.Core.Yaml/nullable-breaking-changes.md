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
