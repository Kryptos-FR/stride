#!/usr/bin/env bash
# Packs Tessera, then builds and runs a game outside this folder that has only Tessera's package and Stride's packages: the
# game's build compiles a tile set and a map with the asset compiler, and the game draws the map in one frame without a
# window. Prints TESSERA-PACKAGES-CHECK OK and exits with 0, or says what failed and exits with 1.
#
# STRIDE_FEED: the folder of the Stride packages to build against; by default the packages of the engine build of the
# checkout Tessera sits in (nuget.config's stride-dev source).
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
tessera=$(cd "$here/../.." && pwd)
stride_feed=${STRIDE_FEED:-$tessera/../../bin/packages}
if [ ! -d "$stride_feed" ]; then
    echo "TESSERA-PACKAGES-CHECK FAILED: no Stride packages in $stride_feed (build the engine's packages, or set STRIDE_FEED)"
    exit 1
fi
stride_feed=$(cd "$stride_feed" && pwd)
tessera_feed=$tessera/bin/packages

work=$(mktemp -d "${TMPDIR:-/tmp}/tessera-packages-check-XXXXXX")
log=$work/check.log
fail() {
    echo "TESSERA-PACKAGES-CHECK FAILED: $1 (log: $log)"
    tail -n 40 "$log" || true
    exit 1
}

project=$tessera/src/ColorRise.Tessera/ColorRise.Tessera.csproj
stride_version=$(dotnet msbuild "$project" -getProperty:TesseraStrideVersion)
tessera_version=$(dotnet msbuild "$project" -getProperty:Version)

echo "Packing Tessera $tessera_version against Stride $stride_version"
dotnet pack "$tessera/ColorRise.Tessera.slnx" >> "$log" 2>&1 || fail "dotnet pack"

# A package of the same version that NuGet already extracted would be used instead of the new one
global_packages=$(dotnet nuget locals global-packages --list | sed 's/^global-packages: //')
rm -rf "$global_packages"/colorrise.tessera "$global_packages"/colorrise.tessera.*

echo "Building the game in $work"
cp -R "$here/Game/." "$work/"
cp "$tessera/global.json" "$work/"
cat > "$work/Directory.Build.props" <<EOF
<Project>
  <PropertyGroup>
    <StrideVersion>$stride_version</StrideVersion>
    <TesseraVersion>$tessera_version</TesseraVersion>
  </PropertyGroup>
</Project>
EOF
cat > "$work/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="stride-dev" value="$stride_feed" />
    <add key="tessera" value="$tessera_feed" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="nuget.org">
      <package pattern="*" />
      <package pattern="Stride.GNU.*" />
      <package pattern="Stride.Mono.*" />
      <package pattern="Stride.Dependencies.*" />
    </packageSource>
    <packageSource key="stride-dev">
      <package pattern="Stride" />
      <package pattern="Stride.*" />
    </packageSource>
    <packageSource key="tessera">
      <package pattern="ColorRise.Tessera" />
      <package pattern="ColorRise.Tessera.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF

dotnet build "$work/PackagesCheck.csproj" >> "$log" 2>&1 || fail "the game's build"

echo "Running the game"
output=$(cd "$work" && env -u DISPLAY dotnet "bin/Debug/net10.0/PackagesCheck.dll" 2>> "$log") || true
echo "$output" >> "$log"
if echo "$output" | grep -qx "TESSERA-PACKAGES-CHECK OK"; then
    echo "TESSERA-PACKAGES-CHECK OK"
    exit 0
fi
fail "the game: $(echo "$output" | grep TESSERA-PACKAGES-CHECK | head -n 1)"
