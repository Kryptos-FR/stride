using ColorRise.Skein.Assets;
using ColorRise.Skein.Assets.Text;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Core.Presentation.Services;

namespace ColorRise.Skein.Editor.Services;

/// <summary>
/// Renames a node in every script that knows it: the scripts of the projects that include the script of the node,
/// whether an editor has them open or not.
/// </summary>
public static class YarnRenameService
{
    /// <summary>
    /// The scripts that can name the nodes of a script: the script, and the others of every project that includes it.
    /// </summary>
    public static IReadOnlyList<YarnScriptViewModel> ScriptsAround(YarnScriptViewModel script)
    {
        ArgumentNullException.ThrowIfNull(script);

        var scripts = new List<YarnScriptViewModel> { script };
        foreach (var project in YarnProjectFiles.ProjectsOf(script.AssetItem))
        {
            foreach (var item in YarnProjectFiles.ScriptsOf(project))
            {
                if (script.Session.GetAssetById(item.Id) is YarnScriptViewModel other && !scripts.Contains(other))
                {
                    scripts.Add(other);
                }
            }
        }

        return scripts;
    }

    /// <summary>
    /// Gives the nodes of a title another title, and makes every jump to them, and every <c>visited</c> call about them,
    /// name the new one, as one step of the undo history.
    /// </summary>
    /// <param name="script">The script of the node.</param>
    /// <returns>Null when the node has the new title; otherwise why it cannot have it, and no text has changed.</returns>
    public static string? Rename(YarnScriptViewModel script, string oldTitle, string newTitle)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(oldTitle);
        ArgumentNullException.ThrowIfNull(newTitle);

        if (newTitle == oldTitle)
            return null;

        var scripts = ScriptsAround(script);
        var documents = scripts.Select(s => YarnDocument.Parse(s.TextBuffer.Text)).ToList();
        var titles = documents.SelectMany(d => d.Nodes).Select(n => n.Title).Where(t => t != oldTitle);
        if (YarnTitles.Validate(newTitle, titles) is { } problem)
            return problem;

        var undoRedo = script.ServiceProvider.Get<IUndoRedoService>();
        using var transaction = undoRedo.CreateTransaction();
        for (var i = 0; i < scripts.Count; i++)
        {
            var document = documents[i];
            var edits = YarnTextPatcher.RenameJumps(document, oldTitle, newTitle)
                .Concat(document.Nodes.Where(n => n.Title == oldTitle).SelectMany(n => YarnTextPatcher.RenameNode(document, n, newTitle)))
                .OrderBy(e => e.Offset)
                .ToList();
            if (edits.Count > 0)
            {
                scripts[i].TextBuffer.Apply(edits, "Rename node");
            }
        }

        undoRedo.SetName(transaction, $"Rename node '{oldTitle}' to '{newTitle}'");
        return null;
    }
}
