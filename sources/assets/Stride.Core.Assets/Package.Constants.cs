// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.Core.Assets;

public partial class Package
{
    /// <summary>
    /// The file extension used for <see cref="Package"/>.
    /// </summary>
    public const string PackageFileExtension = ".sdpkg";

    /// <summary>
    /// The MSBuild item type that makes a file of a project one of its project assets, beside its <c>Compile</c>, <c>None</c>
    /// and <c>AdditionalFiles</c> items: a plugin adds the files of its own item type to it, as
    /// <c>&lt;StrideProjectAsset Include="@(AvaloniaXaml)" /&gt;</c>.
    /// </summary>
    public const string ProjectAssetItemType = "StrideProjectAsset";

    /// <summary>
    /// Whether the items of a type can be project assets of a project (the extension of a file then decides).
    /// </summary>
    internal static bool IsProjectAssetItemType(string itemType)
        => itemType is "Compile" or "None" or "AdditionalFiles" or ProjectAssetItemType;
}
