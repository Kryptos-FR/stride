// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Stride.Core.Translation;
using Stride.PlayMode.Protocol;

namespace Stride.Editor.PlayMode;

/// <summary>
/// How the editor says that the game refused a live edit, in the Output pane.
/// </summary>
[Experimental("STREXP012")]
public static class PlayEditText
{
    /// <summary>
    /// The line of the Output pane for a refused edit: "The game refused the edit of Player > Speed: the setter threw...".
    /// </summary>
    /// <param name="answer">The edit and its answer.</param>
    /// <param name="entityName">The name of the edited entity, if known.</param>
    public static string DescribeRefusal(PlayEditAnswer answer, string? entityName)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var edit = answer.Edit;
        var target = string.IsNullOrEmpty(entityName) ? edit.PathText : $"{entityName} > {edit.PathText}";
        var what = edit.Operation switch
        {
            PlayEditOperation.Add or PlayEditOperation.Insert => Tr._p("Play", "The game refused the item added to {0}: {1}"),
            PlayEditOperation.Remove => Tr._p("Play", "The game refused to remove {0}: {1}"),
            PlayEditOperation.Move => Tr._p("Play", "The game refused to move {0}: {1}"),
            _ => Tr._p("Play", "The game refused the edit of {0}: {1}"),
        };
        var error = answer.Result.Error ?? Tr._p("Play", "no reason given");
        return string.Format(what, target, error) + Tr._p("Play", " The game goes on with the value it had.");
    }
}
