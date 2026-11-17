// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

// The play agent and its snapshot codec can still change between minor versions (docs/build/plugins.md)
[assembly: Experimental("STREXP012")]
[assembly: InternalsVisibleTo("Stride.PlayMode.Tests" + Stride.PublicKeys.Default)]
