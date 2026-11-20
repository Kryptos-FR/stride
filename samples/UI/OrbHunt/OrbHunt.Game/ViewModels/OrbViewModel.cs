// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Windows.Input;
using Avalonia.Media;

namespace OrbHunt.ViewModels;

/// <summary>
/// An orb that the HUD draws as a control, or the ring an orb leaves when it pops (<see cref="IsPop"/>). The game moves
/// it every frame; a press on the orb pops it.
/// </summary>
/// <param name="fill">The brush of the orb, or of its ring.</param>
/// <param name="glow">The brush of the light around the orb.</param>
/// <param name="popCommand">What a press on the orb does; <c>null</c> for a ring.</param>
public sealed class OrbViewModel(IBrush fill, IBrush glow, bool isBomb, ICommand? popCommand) : ObservableObject
{
    public IBrush Fill { get; } = fill;

    public IBrush Glow { get; } = glow;

    /// <summary>
    /// Gets whether the orb is a red one, which costs a life.
    /// </summary>
    public bool IsBomb { get; } = isBomb;

    /// <summary>
    /// Gets whether this is the ring of an orb that popped, which the player can't press.
    /// </summary>
    public bool IsPop => PopCommand == null;

    public ICommand? PopCommand { get; } = popCommand;

    /// <summary>
    /// Gets or sets the left of the orb's square, its light included, in the units of the HUD.
    /// </summary>
    public double Left { get; set => SetField(ref field, value); }

    /// <summary>
    /// Gets or sets the top of the orb's square, its light included, in the units of the HUD.
    /// </summary>
    public double Top { get; set => SetField(ref field, value); }

    /// <summary>
    /// Gets or sets the size of the square: twice the size of the orb, for its light, or the size of the ring.
    /// </summary>
    public double Size { get; set => SetField(ref field, value); }

    public double Opacity { get; set => SetField(ref field, value); }

    /// <summary>
    /// Gets or sets the opacity of the light around the orb: the red ones pulse.
    /// </summary>
    public double GlowOpacity { get; set => SetField(ref field, value); } = 1;
}
