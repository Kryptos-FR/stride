# Stride.Core

Core assembly for all Stride assemblies. Provides foundational utilities, base types, and infrastructure used throughout the engine.

- Component model (`ComponentBase`, `IComponent`) with reference counting
- Service registry (`ServiceRegistry`, `IServiceRegistry`) for dependency injection
- Dynamic property system (`PropertyContainer`, `PropertyKey`)
- Serialization attributes (`[DataContract]`, `[DataMember]`) for the Stride serialization framework
- Diagnostics and logging (`ILogger`, `LogMessage`)
- Platform detection (`Platform`, `PlatformType`)
