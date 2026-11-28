// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.Core.Assets.Editor.Services;

/// <summary>
/// Shows the <em>Template packages</em> window, where the user adds and removes third-party template packages. Registered by
/// the editors that have the window (the cross-platform Game Studio); the start screen offers its button only when it is.
/// </summary>
public interface ITemplatePackagesService
{
    /// <summary>
    /// Shows the window over the start screen and returns once it is closed.
    /// </summary>
    Task ShowAsync();
}
