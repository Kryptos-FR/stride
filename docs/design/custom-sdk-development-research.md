# Custom MSBuild SDK Development: Research & Best Practices

**Document Purpose**: Research findings on how to properly structure custom MSBuild SDKs based on analysis of Microsoft's official SDK implementations.

**Date**: December 29, 2025  
**Author**: SDK Migration Team  
**Status**: Active Research

---

## Executive Summary

When developing custom MSBuild SDKs that extend Microsoft.NET.Sdk, the **import order** is critical for proper functionality. Incorrect ordering causes:
- Missing framework references ("System.Object not defined")
- Broken target dependencies (BeforeTargets/AfterTargets references to non-existent targets)
- Silent compilation failures

**Key Finding**: Microsoft.NET.Sdk.targets must be imported **in the middle** - after property configuration but **before** custom targets that reference SDK targets.

---

## Research Methodology

### Analyzed SDKs
1. **Microsoft.NET.Sdk** (Base SDK)
   - Location: `C:\Program Files\dotnet\sdk\10.0.101\Sdks\Microsoft.NET.Sdk\`
   - Role: Foundation for all .NET projects

2. **Microsoft.NET.Sdk.Web** (Web Applications SDK)
   - Location: `C:\Program Files\dotnet\sdk\10.0.101\Sdks\Microsoft.NET.Sdk.Web\`
   - Role: Extends base SDK with web-specific features (Razor, Publish, etc.)
   - **Key Learning**: Shows proven pattern for extending Microsoft.NET.Sdk

### Analysis Approach
- Examined Sdk.props and Sdk.targets structure
- Traced import chains through SDK files
- Compared with problematic Stride.Sdk implementation
- Documented import order patterns

---

## Microsoft.NET.Sdk.Web Architecture

### File Structure
```
Microsoft.NET.Sdk.Web/
├── Sdk/
│   ├── Sdk.props           → Delegates to Targets/
│   └── Sdk.targets         → Delegates to Targets/
└── Targets/
    ├── Sdk.Server.props    → Actual implementation
    └── Sdk.Server.targets  → Actual implementation
```

### Import Order - Props Phase

**File**: `Microsoft.NET.Sdk.Web\Targets\Sdk.Server.props`

```xml
<Project>
  <!-- 1. Set custom properties FIRST -->
  <PropertyGroup>
    <UsingMicrosoftNETSdkWeb>true</UsingMicrosoftNETSdkWeb>
    <StaticWebAssetProjectMode>Root</StaticWebAssetProjectMode>
    <DebugSymbols Condition="'$(DebugSymbols)' == ''">true</DebugSymbols>
  </PropertyGroup>

  <!-- 2. Import base SDK props -->
  <Import Sdk="Microsoft.NET.Sdk" Project="Sdk.props" />

  <!-- 3. Import supplementary SDK props -->
  <Import Sdk="Microsoft.NET.Sdk.Razor" Project="Sdk.props" />
  <Import Sdk="Microsoft.NET.Sdk.Web.ProjectSystem" Project="Sdk.props" />
  <Import Sdk="Microsoft.NET.Sdk.Publish" Project="Sdk.props" />

  <!-- 4. Add framework references -->
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
</Project>
```

### Import Order - Targets Phase

**File**: `Microsoft.NET.Sdk.Web\Targets\Sdk.Server.targets`

```xml
<Project>
  <!-- 1. Import BeforeCommon targets (pre-SDK hooks) -->
  <Import Sdk="Microsoft.NET.Sdk.Web.ProjectSystem" 
          Project="..\targets\Microsoft.NET.Sdk.Web.BeforeCommon.targets" />

  <!-- 2. ⚠️ CRITICAL: Import Microsoft.NET.Sdk.targets HERE (in the middle) -->
  <Import Sdk="Microsoft.NET.Sdk" Project="Sdk.targets" />

  <!-- 3. Set properties that depend on SDK evaluation -->
  <PropertyGroup>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
  </PropertyGroup>

  <!-- 4. Import supplementary targets (can now use BeforeTargets/AfterTargets) -->
  <Import Sdk="Microsoft.NET.Sdk.Razor" Project="Sdk.targets" />
  <Import Sdk="Microsoft.NET.Sdk.Web.ProjectSystem" Project="Sdk.targets" />
  <Import Sdk="Microsoft.NET.Sdk.Publish" Project="Sdk.targets" />

  <!-- 5. Define custom targets that hook into SDK targets -->
  <ItemGroup>
    <RuntimeHostConfigurationOption Include="..." />
  </ItemGroup>
</Project>
```

---

## Key Insights: Why Order Matters

### Problem 1: Framework References Missing

**Symptom**: "Predefined type 'System.Object' is not defined"

**Cause**: When Microsoft.NET.Sdk.targets is imported too late, framework reference setup happens before custom properties are configured.

**Solution**: Import Microsoft.NET.Sdk.targets **after** setting custom properties but **before** defining targets.

### Problem 2: Target References Don't Exist

**Symptom**: "The target 'CoreCompile' does not exist in the project"

**Cause**: When custom targets use `BeforeTargets="CoreCompile"` but CoreCompile isn't defined yet during import evaluation.

**Explanation**:
- MSBuild evaluates `BeforeTargets`/`AfterTargets` attributes during **import phase**
- If the referenced target doesn't exist, MSBuild issues a warning and ignores the attribute
- Even though target execution happens later, the hook is already broken

**Solution**: Import Microsoft.NET.Sdk.targets **before** custom targets that reference SDK targets.

### Problem 3: Empty Fallback Targets

**Pattern**: Old Stride system defined empty targets at the top:
```xml
<Target Name="Build"/>
<Target Name="Clean"/>
```

**Purpose**: 
- Provides fallbacks for projects that skip compilation
- Gets overridden by real targets when Microsoft.NET.Sdk.targets imports later
- Ensures target always exists even if compilation is disabled

**Applicability**: Still useful for projects with `LanguageTargets` override to disable build.

---

## Correct Import Order Pattern

### Sdk.props Structure
```xml
<Project>
  <!-- 1. SDK Detection & Version -->
  <PropertyGroup>
    <UsingCustomSdk>true</UsingCustomSdk>
    <CustomSdkVersion>1.0.0</CustomSdkVersion>
  </PropertyGroup>

  <!-- 2. Framework Definitions -->
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <!-- 3. Custom Props Files (property-only, no targets) -->
  <Import Project="Custom.Platforms.props" />
  <Import Project="Custom.Runtime.props" Condition="'$(CustomRuntime)' == 'true'" />

  <!-- 4. Import Microsoft.NET.Sdk.props -->
  <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />

  <!-- 5. Property Overrides (after SDK evaluation) -->
  <PropertyGroup>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
</Project>
```

### Sdk.targets Structure
```xml
<Project>
  <!-- 1. Empty Fallback Targets -->
  <Target Name="Build"/>
  <Target Name="Clean"/>
  <Target Name="Publish"/>

  <!-- 2. Property Configuration (before SDK) -->
  <PropertyGroup>
    <OutputType Condition="'$(TargetFramework)' == 'net10.0-android'">Library</OutputType>
    <CustomCompilerEnable Condition="...">false</CustomCompilerEnable>
  </PropertyGroup>

  <!-- 3. ⚠️ CRITICAL: Import Microsoft.NET.Sdk.targets HERE -->
  <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" Condition="'$(ProjectType)' != 'Cpp'" />

  <!-- 4. Import Custom Targets (can now use BeforeTargets/AfterTargets) -->
  <Import Project="Custom.Runtime.targets" Condition="'$(CustomRuntime)' == 'true'" />
  <Import Project="Custom.AssemblyProcessor.targets" />
  <Import Project="Custom.Packaging.targets" />

  <!-- 5. Custom Targets & Hooks -->
  <Target Name="_CustomWorkaround" BeforeTargets="GetPackagingOutputs">
    <!-- Can safely reference GetPackagingOutputs - it exists now -->
  </Target>
</Project>
```

---

## Common Pitfalls

### ❌ Anti-Pattern 1: SDK Targets Imported First
```xml
<!-- WRONG: Imports SDK before setting custom properties -->
<Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />
<PropertyGroup>
  <OutputType>Library</OutputType>  <!-- Too late! -->
</PropertyGroup>
```

**Problem**: SDK evaluates before custom properties are set, breaking framework references.

### ❌ Anti-Pattern 2: SDK Targets Imported Last
```xml
<Import Project="Custom.AssemblyProcessor.targets" />  <!-- Uses BeforeTargets="CoreCompile" -->
<Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />  <!-- CoreCompile defined here -->
```

**Problem**: Custom targets reference CoreCompile before it's defined, breaking target hooks.

### ❌ Anti-Pattern 3: Properties in Targets Files
```xml
<!-- In Custom.Something.targets -->
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>  <!-- Should be in .props -->
</PropertyGroup>
```

**Problem**: Properties evaluated in targets phase are too late for SDK framework setup.

### ✅ Correct Pattern: Sandwich Structure
```xml
<!-- Properties first -->
<PropertyGroup>
  <OutputType>Library</OutputType>
</PropertyGroup>

<!-- SDK in the middle -->
<Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />

<!-- Custom targets last -->
<Import Project="Custom.AssemblyProcessor.targets" />
```

---

## Debugging Import Order Issues

### Diagnostic Commands

**Check Property Evaluation Order:**
```powershell
dotnet msbuild Project.csproj /pp:preprocessed.xml
```
Shows fully evaluated project after all imports.

**Check Target Dependency Chain:**
```powershell
dotnet msbuild Project.csproj /t:Build /v:diag > build.log 2>&1
```
Search for "does not exist in the project" warnings.

**Verify Framework References:**
```powershell
dotnet msbuild Project.csproj /t:CoreCompile /v:n 2>&1 | Select-String "error CS0518"
```
"System.Object not defined" = framework reference issue.

### Warning Signs

1. **"The target 'X' does not exist in the project"**
   - Importing target files before Microsoft.NET.Sdk.targets
   - Solution: Move SDK import earlier

2. **"Predefined type 'System.Object' is not defined"**
   - Properties not set before Microsoft.NET.Sdk.targets
   - Solution: Move property setup before SDK import

3. **"Build succeeded" but no DLL output**
   - CoreCompile not in dependency chain
   - Check if target references are broken

---

## Stride.Sdk Application

### Before (Incorrect)
```xml
<!-- Stride.Sdk\Sdk\Sdk.targets - WRONG ORDER -->
<Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />  <!-- First -->
<Import Project="Stride.AssemblyProcessor.targets" />      <!-- Second - broken! -->
```

### After (Correct - following Microsoft.NET.Sdk.Web pattern)
```xml
<!-- Stride.Sdk\Sdk\Sdk.targets - CORRECT ORDER -->
<Target Name="Build"/>  <!-- Fallback -->

<PropertyGroup>
  <OutputType Condition="...">Library</OutputType>
</PropertyGroup>

<Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />  <!-- Middle -->

<Import Project="Stride.Runtime.targets" />                <!-- After SDK -->
<Import Project="Stride.AssemblyProcessor.targets" />      <!-- Can use BeforeTargets now -->
<Import Project="Stride.Packaging.targets" />
```

---

## Recommendations for Custom SDK Development

1. **Study Official SDKs First**
   - Examine Microsoft.NET.Sdk.Web source code
   - Understand the sandwich pattern (props → SDK → custom targets)
   - Copy proven patterns, don't reinvent

2. **Separate Concerns**
   - Properties go in .props files
   - Targets go in .targets files
   - Don't mix them

3. **Import Order Checklist**
   ```
   Sdk.props:
   ☐ Custom properties
   ☐ Import Microsoft.NET.Sdk.props
   ☐ Property overrides
   
   Sdk.targets:
   ☐ Empty fallback targets
   ☐ Property configuration
   ☐ Import Microsoft.NET.Sdk.targets  ← CRITICAL POSITION
   ☐ Import custom .targets files
   ☐ Define custom targets
   ```

4. **Test Systematically**
   - Build with `/pp` to verify import order
   - Build with `/v:diag` to catch "does not exist" warnings
   - Test with clean cache (`Remove-Item ~/.nuget/packages/yoursdk`)

5. **Document Import Order**
   - Add comments explaining why SDK is imported where it is
   - Reference this research document
   - Save future developers debugging time

---

## References

- **Microsoft.NET.Sdk**: `%ProgramFiles%\dotnet\sdk\{version}\Sdks\Microsoft.NET.Sdk\`
- **Microsoft.NET.Sdk.Web**: `%ProgramFiles%\dotnet\sdk\{version}\Sdks\Microsoft.NET.Sdk.Web\`
- **MSBuild Import Order**: https://learn.microsoft.com/en-us/visualstudio/msbuild/how-to-use-project-sdk
- **Custom SDK Development**: https://learn.microsoft.com/en-us/visualstudio/msbuild/how-to-use-project-sdk#write-your-own-sdk

---

## Appendix: Full Microsoft.NET.Sdk.Web Inspection

### Sdk.Server.props (Full Content)
```xml
<Project ToolsVersion="14.0">
  <PropertyGroup>
    <UsingMicrosoftNETSdkWeb>true</UsingMicrosoftNETSdkWeb>
    <EnableRazorSdkContent>true</EnableRazorSdkContent>
    <DebugSymbols Condition="'$(DebugSymbols)' == ''">true</DebugSymbols>
    <StaticWebAssetProjectMode>Root</StaticWebAssetProjectMode>
    <StaticWebAssetBasePath>/</StaticWebAssetBasePath>
  </PropertyGroup>

  <!-- Chain to base SDK -->
  <Import Sdk="Microsoft.NET.Sdk" Project="Sdk.props" />
  <Import Sdk="Microsoft.NET.Sdk.Razor" Project="Sdk.props" />
  <Import Sdk="Microsoft.NET.Sdk.Web.ProjectSystem" Project="Sdk.props" />
  <Import Sdk="Microsoft.NET.Sdk.Publish" Project="Sdk.props" />

  <!-- Framework references -->
  <ItemGroup Condition="'$(DisableImplicitFrameworkReferences)' != 'true' And 
                        '$(TargetFrameworkIdentifier)' == '.NETCoreApp' And 
                        '$(_TargetFrameworkVersionWithoutV)' >= '3.0'">
    <FrameworkReference Include="Microsoft.AspNetCore.App" IsImplicitlyDefined="true" />
  </ItemGroup>
</Project>
```

### Sdk.Server.targets (Structure)
```xml
<Project ToolsVersion="14.0">
  <!-- BeforeCommon hooks -->
  <Import Sdk="Microsoft.NET.Sdk.Web.ProjectSystem" 
          Project="..\targets\Microsoft.NET.Sdk.Web.BeforeCommon.targets" />

  <!-- CRITICAL: Base SDK imported in the middle -->
  <Import Sdk="Microsoft.NET.Sdk" Project="Sdk.targets" />

  <!-- Post-SDK property configuration -->
  <PropertyGroup>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
  </PropertyGroup>

  <!-- Supplementary SDK targets -->
  <Import Sdk="Microsoft.NET.Sdk.Razor" Project="Sdk.targets" />
  <Import Sdk="Microsoft.NET.Sdk.Web.ProjectSystem" Project="Sdk.targets" />
  <Import Sdk="Microsoft.NET.Sdk.Publish" Project="Sdk.targets" />

  <!-- Custom configuration options -->
  <ItemGroup>
    <RuntimeHostConfigurationOption Include="Microsoft.AspNetCore.SignalR.Hub.IsCustomAwaitableSupported" 
                                    Condition="'$(SignalRCustomAwaitableSupport)' != ''"
                                    Value="$(SignalRCustomAwaitableSupport)" />
  </ItemGroup>
</Project>
```

---

**Document Status**: Initial research complete. Patterns identified and applied to Stride.Sdk.  
**Next Steps**: Validate Stride.Sdk implementation against these patterns, update as needed.
