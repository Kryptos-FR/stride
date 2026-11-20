// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.ComponentModel;
using Avalonia.Controls;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Rendering;

namespace Stride.AvaloniaUI;

/// <summary>
/// Shows an Avalonia view over the game, drawn by Stride: an alternative to <c>UIComponent</c> for games whose UI is
/// written with Avalonia (XAML, controls, styles and bindings).
/// </summary>
/// <remarks>
/// <para>
/// The view is <see cref="Content"/>, or an instance of <see cref="ViewType"/> created when the component enters a scene.
/// It covers the back buffer, laid out at <see cref="Resolution"/> the way <see cref="ResolutionStretch"/> says, like a
/// full screen <c>UIComponent</c>. Where no control is drawn the game shows through and gets the input.
/// </para>
/// <para>
/// The placement properties use the names and meaning of the <c>UIComponent</c> ones, so a game can switch between
/// the two UI systems without learning new ones.
/// </para>
/// </remarks>
[DataContract("AvaloniaUIComponent")]
[Display("Avalonia UI", Expand = ExpandRule.Once)]
[DefaultEntityComponentProcessor(typeof(AvaloniaUIProcessor), ExecutionMode = ExecutionMode.Runtime)]
[ComponentOrder(9810)]
[ComponentCategory("UI")]
public sealed class AvaloniaUIComponent : ActivableEntityComponent
{
    /// <summary>
    /// The default virtual resolution, the one of <c>UIComponent</c>.
    /// </summary>
    public static readonly Vector2 DefaultResolution = new(1280, 720);

    /// <summary>
    /// Gets or sets the type of the view to show when <see cref="Content"/> is not set: a control with a public
    /// parameterless constructor, such as a <c>UserControl</c> written in XAML.
    /// </summary>
    /// <remarks>
    /// An assembly-qualified name (<c>MyGame.Views.MainMenu, MyGame</c>), or a full name looked up in the game's
    /// registered assemblies.
    /// </remarks>
    /// <userdoc>The type of the Avalonia view to show, such as MyGame.Views.MainMenu, MyGame.</userdoc>
    [DataMember(10)]
    [Display("View type")]
    [DefaultValue(null)]
    public string? ViewType { get; set; }

    /// <summary>
    /// Gets or sets the view to show. It takes precedence over <see cref="ViewType"/>.
    /// </summary>
    [DataMemberIgnore]
    public Control? Content { get; set; }

    /// <summary>
    /// Gets or sets the virtual resolution of the view, in Avalonia's logical units.
    /// </summary>
    /// <userdoc>The resolution the view is designed for. It is scaled to the screen as Resolution Stretch says.</userdoc>
    [DataMember(30)]
    [Display("Resolution")]
    public Vector2 Resolution { get; set; } = DefaultResolution;

    /// <summary>
    /// Gets or sets how <see cref="Resolution"/> is fitted to the screen.
    /// </summary>
    /// <remarks>
    /// The adaptable modes keep one dimension and give the view the screen's ratio, rendered at the screen's pixel size.
    /// <see cref="ResolutionStretch.FixedWidthFixedHeight"/> keeps both and stretches the view to the screen.
    /// </remarks>
    /// <userdoc>How the resolution is fitted to the screen.</userdoc>
    [DataMember(40)]
    [Display("Resolution Stretch")]
    [DefaultValue(ResolutionStretch.FixedWidthAdaptableHeight)]
    public ResolutionStretch ResolutionStretch { get; set; } = ResolutionStretch.FixedWidthAdaptableHeight;

    /// <summary>
    /// Gets or sets the order of the view among the Avalonia views: higher is drawn on top and gets the input first.
    /// </summary>
    /// <userdoc>Views with a higher draw order are drawn on top of the others and get the input first.</userdoc>
    [DataMember(50)]
    [Display("Draw order")]
    [DefaultValue(0)]
    public int DrawOrder { get; set; }

    /// <summary>
    /// Gets the Avalonia top level that hosts the view, while the component is in a running scene.
    /// </summary>
    /// <remarks>Use it to reach Avalonia's focus manager, resources or styles of this view.</remarks>
    [DataMemberIgnore]
    public TopLevel? TopLevel { get; internal set; }
}
