// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.Core.Packages;

/// <summary>
/// What <see cref="NugetStore.AddPackageFile"/> did.
/// </summary>
public enum PackageFileAddition
{
    /// <summary>The package was added to the global packages folder.</summary>
    Added,

    /// <summary>That id and version was installed already, from the same file: nothing changed.</summary>
    AlreadyInstalled,

    /// <summary>That id and version was installed already, with other content: nothing changed.</summary>
    InstalledWithOtherContent,
}
