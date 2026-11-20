// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Windows.Input;

namespace OrbHunt.ViewModels;

/// <summary>
/// A command that runs an action, for the buttons of the views.
/// </summary>
public sealed class RelayCommand(Action execute) : ICommand
{
    // Always runs: the views hide the buttons that can't be used
    public event EventHandler? CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}
