# Stride.Core.CompilerServices

Roslyn-based source generators and analyzers for `Stride.Core` and its dependents. Runs at compile time to generate boilerplate code.

- Source generators for serialization infrastructure (type descriptors, member accessors)
- Compile-time analyzers for `[DataContract]` / `[DataMember]` usage correctness
- Reduces runtime reflection overhead by generating code ahead of time
