// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stride.Core.Assets.Editor.Services;

/// <summary>The reason a build was triggered.</summary>
public enum BuildTrigger
{
    /// <summary>Automatically triggered when a session is opened.</summary>
    SessionOpen,
    /// <summary>Triggered by an explicit user action.</summary>
    Manual,
    /// <summary>Triggered because one or more assets changed on disk.</summary>
    AssetChanged,
}

/// <summary>
/// This interface represents a service that build assets.
/// </summary>
public interface IBuildService
{
    /// <summary>
    /// Raised when an asset has been built.
    /// </summary>
    event EventHandler<AssetBuiltEventArgs> AssetBuilt;

    /// <summary>
    /// Compiles all stale assets in the session asynchronously.
    /// </summary>
    /// <param name="trigger">Why the build was triggered.</param>
    /// <param name="ct">Cancellation token.</param>
    Task BuildProjectAsync(BuildTrigger trigger, CancellationToken ct = default) => Task.CompletedTask;
}
