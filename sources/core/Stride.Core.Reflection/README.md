# Stride.Core.Reflection

Type reflection and descriptor utilities for the Stride game engine. Used by the editor and serialization systems for runtime type introspection.

- `TypeDescriptorFactory` — generates and caches descriptors for types
- `ITypeDescriptor`, `ObjectDescriptor`, `CollectionDescriptor` — typed metadata
- `IMemberDescriptor` — unified access to properties and fields
- `AttributeRegistry` — attach and query attributes on types at runtime
- Shadow object support for non-destructive property overrides
