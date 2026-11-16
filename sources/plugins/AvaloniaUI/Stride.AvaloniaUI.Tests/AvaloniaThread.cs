// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Stride.AvaloniaUI.Tests;

/// <summary>
/// The thread of the tests that use Avalonia: Avalonia is set up once per process, and its UI thread is the one that set
/// it up, like the game thread of a game.
/// </summary>
internal static class AvaloniaThread
{
    private static readonly BlockingCollection<Action> Queue = [];

    static AvaloniaThread()
    {
        var thread = new Thread(() =>
        {
            foreach (var action in Queue.GetConsumingEnumerable())
                action();
        })
        {
            IsBackground = true,
            Name = "Avalonia UI thread",
        };
        thread.Start();
    }

    /// <summary>
    /// Runs an action on the thread and waits for it.
    /// </summary>
    public static void Run(Action action)
    {
        ExceptionDispatchInfo? error = null;
        using var done = new ManualResetEventSlim();
        Queue.Add(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                error = ExceptionDispatchInfo.Capture(e);
            }
            finally
            {
                done.Set();
            }
        });
        done.Wait();
        error?.Throw();
    }
}
