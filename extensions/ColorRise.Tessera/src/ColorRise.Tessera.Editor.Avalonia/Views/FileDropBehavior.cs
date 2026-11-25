using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Stride.Core.Assets.Editor.Avalonia.DragDrop;
using Stride.Core.IO;

namespace ColorRise.Tessera.Editor.Avalonia.Views;

/// <summary>
/// Gives the files dropped on a control from the file manager to a command, as an <see cref="IReadOnlyList{T}"/> of
/// <see cref="UFile"/>. A drag of the editor's own items is left to the other drop behaviours of the control or its children.
/// </summary>
internal sealed class FileDropBehavior
{
    /// <summary>
    /// The command that gets the dropped files; its <see cref="ICommand.CanExecute"/> says whether they can be dropped.
    /// </summary>
    public static readonly AttachedProperty<ICommand?> CommandProperty = AvaloniaProperty.RegisterAttached<FileDropBehavior, Control, ICommand?>("Command");

    static FileDropBehavior()
    {
        CommandProperty.Changed.AddClassHandler<Control>(CommandChanged);
    }

    private FileDropBehavior()
    {
    }

    /// <summary>
    /// The command that gets the files dropped on the control.
    /// </summary>
    public static ICommand? GetCommand(Control control) => control.GetValue(CommandProperty);

    /// <summary>
    /// Sets the command that gets the files dropped on the control.
    /// </summary>
    public static void SetCommand(Control control, ICommand? command) => control.SetValue(CommandProperty, command);

    private static void CommandChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        // The handlers also see the events a child marked handled: a child's drop behaviour refuses the files it does not take
        control.RemoveHandler(DragDrop.DragOverEvent, DragOver);
        control.RemoveHandler(DragDrop.DropEvent, Drop);
        if (e.NewValue is not null)
        {
            DragDrop.SetAllowDrop(control, true);
            control.AddHandler(DragDrop.DragOverEvent, DragOver, RoutingStrategies.Bubble, handledEventsToo: true);
            control.AddHandler(DragDrop.DropEvent, Drop, RoutingStrategies.Bubble, handledEventsToo: true);
        }
    }

    // A drag of the editor's own items is not a drop of files, even when it also carries files
    private static IReadOnlyList<UFile> FilesOf(DragEventArgs e)
        => AssetDragDropHelper.GetItems(e.DataTransfer).Count > 0 ? [] : AssetDragDropHelper.GetFiles(e.DataTransfer);

    private static void DragOver(object? sender, DragEventArgs e)
    {
        if (sender is not Control control || FilesOf(e) is not { Count: > 0 } files)
        {
            return;
        }
        e.DragEffects = GetCommand(control)?.CanExecute(files) == true ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private static void Drop(object? sender, DragEventArgs e)
    {
        if (sender is not Control control || FilesOf(e) is not { Count: > 0 } files)
        {
            return;
        }
        if (GetCommand(control) is { } command && command.CanExecute(files))
        {
            command.Execute(files);
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
        e.Handled = true;
    }
}
