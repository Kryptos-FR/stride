// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Runtime.CompilerServices;

#pragma warning disable 436 // Stride.PublicKeys is defined in multiple assemblies

[assembly: InternalsVisibleTo("Stride.AvaloniaUI.Tests" + Stride.PublicKeys.Default)]
[assembly: InternalsVisibleTo("Stride.AvaloniaUI.Native.Tests" + Stride.PublicKeys.Default)]
// The renderers of the Skia package
[assembly: InternalsVisibleTo("Stride.AvaloniaUI.Skia" + Stride.PublicKeys.Default)]
