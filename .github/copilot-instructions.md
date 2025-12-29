# Stride Game Engine Copilot Instructions

This document provides guidelines and best practices for using GitHub Copilot and Copilot Chat within the Stride Game Engine repository. The goal is to ensure Copilot is used effectively and consistently to maintain code quality, project standards, and team productivity.

## ⚠️ CRITICAL: SDK Development Workflow (Stride.Sdk & Stride.Sdk.Runtime)

When modifying SDK files (`.props`, `.targets`, or SDK project files), you **MUST** follow this exact workflow to ensure changes take effect:

### Required 3-Step Process (DO NOT SKIP ANY STEP):

1. **Modify SDK source files** in `sources/sdk/Stride.Sdk/` or `sources/sdk/Stride.Sdk.Runtime/`
2. **Rebuild the SDK package(s)** with Clean,Pack to `build/packages`:
   ```powershell
   # CRITICAL: Must specify PackageOutputPath to build/packages directory
   # The build system expects SDK packages in build/packages, NOT in bin/Debug
   
   # For Stride.Sdk
   msbuild sources\sdk\Stride.Sdk\Stride.Sdk.csproj /t:Clean,Pack /p:Configuration=Debug /p:PackageOutputPath="$PWD\build\packages"
   
   # For Stride.Sdk.Runtime (if modified)
   msbuild sources\sdk\Stride.Sdk.Runtime\Stride.Sdk.Runtime.csproj /t:Clean,Pack /p:Configuration=Debug /p:PackageOutputPath="$PWD\build\packages"
   ```
3. **Clear NuGet package cache** (CRITICAL - changes won't be visible without this):
   ```powershell
   Remove-Item $env:USERPROFILE\.nuget\packages\stride.sdk -Recurse -Force
   Remove-Item $env:USERPROFILE\.nuget\packages\stride.sdk.runtime -Recurse -Force
   ```

### Why This Matters:

- **MSBuild caches SDKs aggressively** - modifications to source `.props`/`.targets` files are NOT visible until the package is rebuilt AND the cache is cleared
- **Skipping cache clearing** will cause you to test OLD code, leading to phantom bugs and wasted debugging time
- **Use Clean,Pack** instead of just Pack to ensure a fresh build

### Common Mistakes to Avoid:

- ❌ Modifying `.props`/`.targets` and immediately testing without rebuild
- ❌ **Forgetting `/p:PackageOutputPath` - packages go to bin/Debug instead of build/packages, causing NuGet to use OLD packages**ing SDK
- ❌ Rebuilding SDK but forgetting to clear cache
- ❌ Only clearing Stride.Sdk cache when Stride.Sdk.Runtime was also modified
- ❌ Using `/t:Pack` without `/t:Clean` - can cause stale files in package

### Verification:
:
```powershell
# 1. Check packages are in build/packages with recent timestamp
Get-ChildItem build\packages\Stride.Sdk*.nupkg | Select-Object Name,LastWriteTime

# 2. Verify the restored package contains your changes + cache clear, verify the changes are in the package:
```powershell
Get-ChildItem $env:USERPROFILE\.nuget\packages\stride.sdk\4.3.0-dev\Sdk\
```

## Coding & Contribution Guidelines

- Prefer concise, well-documented, and idiomatic C# code.
- Do not use `#region` directives; prefer clear, self-documenting code.

## Copilot Pull Request Code Review Instructions

Stride is a game engine project that requires careful code reviews to maintain quality and performance. Please follow these guidelines when reviewing pull requests (PRs):

- Generate a neat and concise Pull Request Overview, highlighting the most important changes.
- Focus reviews on logic, safety, performance, and code consistency with the existing codebase.
- Avoid suggesting large architectural changes in PR reviews.
- Comments on formatting, grammar, or spelling are welcome.
- Minor style or nit-pick comments are acceptable to maintain consistency.
- Do not review auto-generated, third-party code, binary files, or assets.
- If you find a bug or performance issue, suggest a concrete fix in the PR.
- For large PRs (20+ C#/*.cs files), do not attempt a full review, only highlight critical or blocking issues.
- Always consider the context and established patterns in the Stride codebase before making suggestions.

The goal is to minimize noise and maximize helpful, actionable feedback.
