using System.Collections.Specialized;
using System.Text;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.IO;
using Stride.Core.Presentation.Services;
using Stride.Core.Presentation.Windows;

namespace ColorRise.Skein.Editor.Services;

/// <summary>
/// Brings into the session what another program writes to the Yarn scripts: Yarn Spinner's own tools and any text editor
/// work on the same files. Game Studio reloads its other assets itself, and leaves text files to who edits them.
/// </summary>
/// <remarks>
/// A service of the session: <see cref="Of"/>. A script without unsaved changes takes the text of its file, as a step of
/// the undo history that leaves it without unsaved changes. A script with unsaved changes takes it only when the user
/// says so, who is asked once for a content of the file.
/// </remarks>
public sealed class YarnExternalChangeService
{
    private static ReadOnlySpan<byte> Utf8ByteOrderMark => [0xEF, 0xBB, 0xBF];

    private readonly ISessionViewModel session;
    private readonly DirectoryWatcher watcher = new("*" + Assets.YarnScriptAsset.FileExtension);
    private readonly HashSet<string> trackedFolders = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> pending = new(StringComparer.OrdinalIgnoreCase);
    // The contents the user chose not to take, by file: the same content is not asked about again
    private readonly Dictionary<string, string> declined = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource closing = new();
    private CancellationTokenSource delay = new();
    private Task pump = Task.CompletedTask;

    public YarnExternalChangeService(ISessionViewModel session)
    {
        this.session = session;
        watcher.Modified += OnFileModified;
        session.LocalPackages.CollectionChanged += OnPackagesChanged;
        TrackPackages();
    }

    /// <summary>
    /// How long the files stay quiet before the service reads them: a program that writes a file changes it several times.
    /// </summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// The service of a session.
    /// </summary>
    public static YarnExternalChangeService Of(ISessionViewModel session) => session.ServiceProvider.Get<YarnExternalChangeService>();

    /// <summary>
    /// Compares every Yarn script of the session with its file now, and takes what changed.
    /// </summary>
    public Task CheckAsync() => CheckAsync(null);

    /// <summary>
    /// Stops the service when its session closes.
    /// </summary>
    public void Close()
    {
        if (closing.IsCancellationRequested)
            return;

        closing.Cancel();
        delay.Cancel();
        session.LocalPackages.CollectionChanged -= OnPackagesChanged;
        watcher.Modified -= OnFileModified;
        watcher.Dispose();
    }

    private void OnPackagesChanged(object? sender, NotifyCollectionChangedEventArgs e) => TrackPackages();

    private void TrackPackages()
    {
        foreach (var package in session.LocalPackages)
        {
            if (!package.IsEditable || package.Package.RootDirectory is null)
                continue;

            foreach (var folder in package.Package.GetDistinctAssetFolderPaths())
            {
                var path = folder.ToOSPath();
                if (Directory.Exists(path) && trackedFolders.Add(path))
                {
                    watcher.Track(path);
                }
            }
        }
    }

    // Raised from a thread of the watcher
    private void OnFileModified(object? sender, FileEvent e)
    {
        lock (pending)
        {
            pending.Add(Path.GetFullPath(e.FullPath));
        }

        session.Dispatcher.InvokeAsync(Schedule);
    }

    private void Schedule()
    {
        if (closing.IsCancellationRequested)
            return;

        if (pump.IsCompleted)
        {
            pump = session.TrackBackgroundWork(session.Dispatcher.InvokeTask(PumpAsync));
        }
        else
        {
            // A change during the wait starts the wait again
            delay.Cancel();
        }
    }

    private async Task PumpAsync()
    {
        while (!closing.IsCancellationRequested)
        {
            delay = new CancellationTokenSource();
            try
            {
                await Task.Delay(Delay, delay.Token);
            }
            catch (OperationCanceledException)
            {
                continue;
            }

            // The session writes its own files, or is in the middle of a change: the files wait until it is done
            if (IsSessionBusy())
                continue;

            HashSet<string> files;
            lock (pending)
            {
                if (pending.Count == 0)
                    return;

                files = new HashSet<string>(pending, StringComparer.OrdinalIgnoreCase);
                pending.Clear();
            }

            await CheckAsync(files);
        }
    }

    private bool IsSessionBusy()
    {
        var undoRedo = session.ServiceProvider.Get<IUndoRedoService>();
        return session is SessionViewModel { IsSaving: true } || undoRedo.TransactionInProgress || undoRedo.UndoRedoInProgress;
    }

    private async Task CheckAsync(HashSet<string>? files)
    {
        // One at a time: a question to the user may be open
        await gate.WaitAsync();
        try
        {
            foreach (var script in session.AllAssets.OfType<YarnScriptViewModel>().ToList())
            {
                if (closing.IsCancellationRequested)
                    return;

                var path = Path.GetFullPath(script.AssetItem.FullPath.ToOSPath());
                if (files is null || files.Contains(path))
                {
                    await CheckAsync(script, path);
                }
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task CheckAsync(YarnScriptViewModel script, string path)
    {
        if (await ReadAsync(path) is not var (text, hasByteOrderMark) || !IsInSession(script))
            return;

        // The file holds what the session has, or what the session wrote last: what was typed since is an unsaved
        // change, not a change of the file
        if (text == script.TextBuffer.Text || text == script.Asset.SavedText)
        {
            // A script whose text was not read before the file changed has the text of the file already
            script.Asset.HasByteOrderMark = hasByteOrderMark;
            declined.Remove(path);
            return;
        }

        if (script.IsDirty)
        {
            if (declined.TryGetValue(path, out var known) && known == text)
                return;

            var message = $"{script.Url} has been changed outside of Game Studio, and has unsaved changes here.\r\n\r\n"
                + "Do you want to take the text of the file? Undo gives the text of now back. If you keep it, saving overwrites the changes made outside of Game Studio.";
            DialogButtonInfo[] buttons =
            [
                new() { Content = "Reload", Result = 1, IsDefault = true },
                new() { Content = "Keep my changes", Result = 2, IsCancel = true },
            ];
            var answer = await session.ServiceProvider.Get<IEditorDialogService>().MessageBoxAsync(message, buttons, MessageBoxImage.Warning);
            if (answer != 1)
            {
                declined[path] = text;
                return;
            }

            // The script may have gone while the question was open
            if (!IsInSession(script) || closing.IsCancellationRequested)
                return;
        }

        declined.Remove(path);
        script.Asset.HasByteOrderMark = hasByteOrderMark;
        script.TextBuffer.ReloadFromDisk(text);
    }

    private bool IsInSession(YarnScriptViewModel script) => !script.IsDeleted && session.GetAssetById(script.Id) == script;

    private static async Task<(string Text, bool HasByteOrderMark)?> ReadAsync(string path)
    {
        // The program that writes the file may still have it open
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(path);
                var hasByteOrderMark = bytes.AsSpan().StartsWith(Utf8ByteOrderMark);
                return (Encoding.UTF8.GetString(bytes, hasByteOrderMark ? 3 : 0, bytes.Length - (hasByteOrderMark ? 3 : 0)), hasByteOrderMark);
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException)
            {
                // A file that is gone is not a text to take: the script stays as the session has it
                return null;
            }
            catch (IOException) when (attempt < 10)
            {
                await Task.Delay(100);
            }
        }
    }
}
