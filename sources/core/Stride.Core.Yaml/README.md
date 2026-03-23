# Stride.Core.Yaml

YAML serialization library used by the Stride asset pipeline. Handles reading and writing of `.sd*` asset files.

- Full YAML 1.1/1.2 specification support (scanner, parser, emitter)
- Event-based streaming API (`EventReader`) for memory-efficient parsing
- Schema support for type tagging and custom type resolution
- Integration with the Stride serialization framework for asset round-tripping
