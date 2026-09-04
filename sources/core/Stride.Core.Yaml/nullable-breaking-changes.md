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
