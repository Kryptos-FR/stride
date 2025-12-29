# Stride.Sdk

MSBuild SDK for Stride game engine projects.

## Overview

`Stride.Sdk` is a custom MSBuild SDK that simplifies Stride project files by:
- Automatically importing all necessary build logic
- Providing sensible defaults for Stride projects
- Enabling modern SDK-style project files
- Supporting multi-platform and multi-graphics-API builds

## Usage

### SDK-Style Project (Recommended)

```xml
<Project Sdk="Stride.Sdk">
  <PropertyGroup>
    <StrideRuntime>true</StrideRuntime>
  </PropertyGroup>
  
  <ItemGroup>
    <ProjectReference Include="..\OtherProject\OtherProject.csproj" />
  </ItemGroup>
</Project>
```

### Legacy PackageReference

If you need to use `<PackageReference>` instead:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Stride.Sdk" Version="4.3.0" />
  </ItemGroup>
  
  <!-- Your project content -->
</Project>
```

## Configuration

### Properties

| Property | Default | Description |
|----------|---------|-------------|
| `StrideRuntime` | `false` | Enable multi-platform runtime builds |
| `StrideAssemblyProcessor` | `false` | Enable assembly processor |
| `StrideGraphicsApiDependent` | `false` | Build for multiple graphics APIs |
| `StridePublicApi` | `false` | Generate user documentation |
| `StridePackAssets` | `false` | Pack Stride assets into NuGet |

### Target Frameworks

The SDK automatically configures target frameworks based on:
- `StrideRuntime=true`: Multi-targets all platforms in `StridePlatforms`
- Platform-specific builds use appropriate TFMs (net10.0-android, net10.0-ios, etc.)

### Graphics APIs

Supported graphics APIs:
- **Windows**: Direct3D11, Direct3D12, OpenGL, OpenGLES, Vulkan
- **Linux**: OpenGL, Vulkan  
- **UWP**: Direct3D11
- **Android**: OpenGLES, Vulkan
- **iOS**: OpenGLES

## Features

### Multi-Platform Support

Set `StrideRuntime=true` to automatically build for multiple platforms:

```xml
<PropertyGroup>
  <StrideRuntime>true</StrideRuntime>
</PropertyGroup>
```

Control platforms with `StridePlatforms` property (semicolon-separated):
- Windows
- Linux
- Android
- iOS
- UWP

### Assembly Processor

Enable Stride's assembly processor for serialization and module initialization:

```xml
<PropertyGroup>
  <StrideAssemblyProcessor>true</StrideAssemblyProcessor>
  <StrideAssemblyProcessorOptions>--auto-module-initializer --serialization</StrideAssemblyProcessorOptions>
</PropertyGroup>
```

### Graphics API Multi-Targeting

For projects that need to build for multiple graphics APIs:

```xml
<PropertyGroup>
  <StrideGraphicsApiDependent>true</StrideGraphicsApiDependent>
</PropertyGroup>
```

## File Structure

```
Stride.Sdk/
├── Sdk/
│   ├── Sdk.props                          # Main entry point
│   ├── Sdk.targets                        # Main targets
│   ├── Stride.Platforms.props             # Platform detection
│   ├── Stride.Runtime.props               # Multi-platform support
│   ├── Stride.Graphics.props              # Graphics API config
│   ├── Stride.PackageVersion.props        # Version management
│   ├── Stride.AssemblyProcessor.targets   # Assembly processor
│   ├── Stride.GraphicsApi.targets         # Graphics API builds
│   └── Stride.Packaging.targets           # NuGet packaging
└── build/
    ├── Stride.Sdk.props                   # Legacy compatibility
    └── Stride.Sdk.targets                 # Legacy compatibility
```

## Extensibility

### Custom Pre/Post Hooks

Define custom props/targets files:

```xml
<PropertyGroup>
  <CustomBeforeStrideSdkProps>$(MSBuildThisFileDirectory)Custom.Before.props</CustomBeforeStrideSdkProps>
  <CustomAfterStrideSdkTargets>$(MSBuildThisFileDirectory)Custom.After.targets</CustomAfterStrideSdkTargets>
</PropertyGroup>
```

## Migration Guide

See [sdk-modernization-roadmap.md](../../../docs/design/sdk-modernization-roadmap.md) for detailed migration instructions.

## Development

### Building the SDK

```powershell
cd sources/sdk/Stride.Sdk
dotnet pack
```

### Testing Locally

1. Build the SDK package
2. Copy to local NuGet feed
3. Update `global.json` with SDK version
4. Test with a sample project

## Documentation

- [SDK Modernization Research](../../../docs/design/sdk-modernization-research.md)
- [Build Properties Inventory](../../../docs/design/stride-build-properties-inventory.md)
- [SDK Roadmap](../../../docs/design/sdk-modernization-roadmap.md)

## License

MIT License - See [LICENSE.md](../../../LICENSE.md)
