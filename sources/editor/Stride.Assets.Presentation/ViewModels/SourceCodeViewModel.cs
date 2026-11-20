// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Assets.Presentation.Annotations;

namespace Stride.Assets.Presentation.ViewModels;

/// <summary>
/// View model for a <see cref="SourceCodeAsset"/>, such as a script.
/// </summary>
/// <remarks>
/// The text of a source code asset is edited by its editor, outside the action history: the editor reports its unsaved edits with
/// <see cref="SetHasUnsavedText"/>, and the asset is dirty while it has some, as in the WPF editor. The editor writes the file
/// itself, so the session does not write the asset for these edits (its asset item is dirty only for the changes of the
/// action history, such as a rename).
/// <para>
/// The asset also keeps the signature of the file its text was read from. Before the session writes the assets (a rename
/// writes the text to the new file), an asset whose file another program changed takes the text of the file, unless its
/// editor has unsaved edits: so the change is kept whether the editor of the asset is open, closed or was never opened.
/// </para>
/// </remarks>
public abstract class SourceCodeViewModel<TSourceCodeAsset> : AssetViewModel<TSourceCodeAsset>, ISourceCodeViewModel
    where TSourceCodeAsset : SourceCodeAsset
{
    private bool isHistoryDirty;
    // The file that the text was read from or written to, and its signature then; null when the asset has no file to follow
    private string? filePath;
    private FileContentSignature signature;

    protected SourceCodeViewModel(AssetViewModelConstructionParameters parameters)
        : base(parameters)
    {
    }

    /// <summary>
    /// The full path to the source code asset on disk.
    /// </summary>
    public UFile FullPath => AssetItem.FullPath;

    /// <inheritdoc />
    SourceCodeAsset ISourceCodeViewModel.SourceCode => Asset;

    /// <inheritdoc />
    public bool HasUnsavedText { get; private set; }

    /// <inheritdoc />
    public void SetHasUnsavedText(bool value)
    {
        if (HasUnsavedText == value)
            return;

        HasUnsavedText = value;
        OnPropertyChanged(nameof(HasUnsavedText));
        base.UpdateDirtiness(isHistoryDirty || HasUnsavedText);
    }

    /// <inheritdoc/>
    protected internal override void Initialize(ILogger log)
    {
        base.Initialize(log);

        // Keeps the text of a project file in memory: an undo writes it back if the file is deleted
        if (Asset is IProjectAsset && !Directory.Package.Package.IsReadOnly)
            _ = Asset.Text;
        if (!Directory.Package.Package.IsReadOnly)
            CaptureFile();
    }

    /// <inheritdoc />
    protected internal override void OnSessionSaving()
    {
        base.OnSessionSaving();
        if (filePath == null || HasUnsavedText)
            return;

        // The file is read where the text came from: until the session saves a rename, the asset keeps its old file
        var current = FileContentSignature.Capture(filePath);
        if (current == signature || !current.Exists)
            return;

        try
        {
            var text = File.ReadAllText(filePath);
            if (Asset.Text != text)
                Asset.Text = text;
            signature = current;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Another program still holds the file: the session writes the text it has, as before
        }
    }

    /// <inheritdoc />
    protected internal override void OnSessionSaved()
    {
        base.OnSessionSaved();
        // The session wrote the file (or had nothing to write): its file is the one of its current name. With unsaved edits,
        // the editor has not written the file, so the old signature stays and the next save still checks it.
        if (filePath != null && !HasUnsavedText)
            CaptureFile();
    }

    private void CaptureFile()
    {
        filePath = FullPath?.ToOSPath();
        signature = FileContentSignature.Capture(filePath);
    }

    /// <inheritdoc />
    protected override void UpdateDirtiness(bool value)
    {
        // The dirtiness of the action history, combined with the unsaved edits of the editor
        isHistoryDirty = value;
        base.UpdateDirtiness(value || HasUnsavedText);
    }

    /// <inheritdoc />
    protected override void OnDirtyFlagSet()
    {
        // Only the changes of the action history make the session write the file: its editor writes the text itself
        AssetItem.IsDirty = isHistoryDirty;
    }
}

[AssetViewModel<SourceCodeAsset>]
public class SourceCodeAssetViewModel : SourceCodeViewModel<SourceCodeAsset>
{
    public SourceCodeAssetViewModel(AssetViewModelConstructionParameters parameters)
        : base(parameters)
    {
    }
}
