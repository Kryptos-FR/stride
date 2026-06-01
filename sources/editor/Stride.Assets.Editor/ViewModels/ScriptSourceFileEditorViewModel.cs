// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Stride.Assets.Presentation.ViewModels;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.Editors;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Presentation.Commands;
using Stride.Core.Presentation.Services;

namespace Stride.Assets.Editor.ViewModels;

/// <summary>
/// View model for the script source editor. Hosts a plain-text C# buffer with local
/// modified-state tracking and a self-contained per-file save (there is no session Save-All yet).
/// </summary>
[AssetEditorViewModel<ScriptSourceFileViewModel>]
public sealed class ScriptSourceFileEditorViewModel : AssetEditorViewModel<ScriptSourceFileViewModel>
{
    private string savedText;
    private string currentText;
    private bool isModified;
    private int fontSize = 13;
    private FileContentSignature signature;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptSourceFileEditorViewModel"/> class.
    /// </summary>
    /// <param name="asset">The script asset to edit.</param>
    public ScriptSourceFileEditorViewModel(ScriptSourceFileViewModel asset)
        : base(asset)
    {
        savedText = currentText = asset.Asset.Text ?? string.Empty;
        signature = FileContentSignature.Capture(FilePath);
        SaveCommand = new AnonymousTaskCommand(ServiceProvider, SaveAsync, () => IsModified);
    }

    /// <summary>
    /// Gets the initial text content for the editor view to load.
    /// </summary>
    public string Text => currentText;

    /// <summary>
    /// Gets the command that saves the current buffer to disk.
    /// </summary>
    public ICommandBase SaveCommand { get; }

    /// <summary>
    /// Gets a value indicating whether the buffer differs from the last saved content.
    /// </summary>
    public bool IsModified
    {
        get => isModified;
        private set => SetValue(ref isModified, value);
    }

    /// <summary>
    /// Gets or sets the editor font size, clamped to the [8, 72] range.
    /// </summary>
    public int FontSize
    {
        get => fontSize;
        set => SetValue(ref fontSize, Math.Clamp(value, 8, 72));
    }

    /// <summary>
    /// Occurs when the buffer has been reloaded from disk, so the view can refresh the editor.
    /// The event argument carries the reloaded text.
    /// </summary>
    public event EventHandler<string>? TextReloaded;

    private string? FilePath => Asset.AssetItem.FullPath.ToOSPath();

    /// <summary>
    /// Called by the view when the user edits the text.
    /// </summary>
    /// <param name="text">The new buffer content.</param>
    public void OnTextEdited(string text)
    {
        currentText = text ?? string.Empty;
        IsModified = !string.Equals(currentText, savedText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reloads the buffer from disk when the file changed externally and there are no unsaved edits.
    /// Intended to be called by the view when the editor is (re)activated.
    /// </summary>
    public void CheckExternalChanges()
    {
        if (IsModified)
            return;

        var path = FilePath;
        var current = FileContentSignature.Capture(path);
        if (current == signature)
            return;

        var text = !string.IsNullOrEmpty(path) && File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        savedText = currentText = text;
        signature = current;
        IsModified = false;
        TextReloaded?.Invoke(this, currentText);
    }

    private async Task SaveAsync()
    {
        var path = FilePath;
        if (string.IsNullOrEmpty(path))
            return;

        // Safe-save guard: warn before overwriting an external change.
        var current = FileContentSignature.Capture(path);
        if (current.Exists && current != signature)
        {
            var dialog = ServiceProvider.Get<IDialogService>();
            var result = await dialog.MessageBoxAsync(
                $"The file \"{Path.GetFileName(path)}\" has changed on disk since it was opened.\n\nOverwrite it with your changes?",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (result != MessageBoxResult.OK)
                return;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(path, currentText, Encoding.UTF8);

        // Keep the in-memory asset text in sync for other consumers.
        Asset.Asset.Text = currentText;
        savedText = currentText;
        IsModified = false;
        signature = FileContentSignature.Capture(path);
    }
}
