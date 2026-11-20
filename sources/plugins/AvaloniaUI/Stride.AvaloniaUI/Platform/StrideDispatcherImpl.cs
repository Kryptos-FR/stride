// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using Avalonia.Threading;

namespace Stride.AvaloniaUI.Platform;

/// <summary>
/// Avalonia's dispatcher loop, run by the game loop: Avalonia's jobs and timers run when <see cref="Pump"/> is called.
/// </summary>
/// <remarks>
/// The game thread is Avalonia's UI thread, so a game never blocks on Avalonia and Avalonia never waits for the game.
/// <see cref="Signal"/> can be called from any thread (a job posted from a worker); the jobs run at the next pump.
/// </remarks>
internal sealed class StrideDispatcherImpl : IDispatcherImpl
{
    // Jobs that post new jobs are run again in the same pump, up to this many rounds; the rest wait for the next frame
    private const int MaxRoundsPerPump = 8;

    private readonly Thread thread;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly object timerLock = new();
    private long? dueTime;
    private int signaled;

    public StrideDispatcherImpl(Thread thread)
    {
        this.thread = thread;
    }

    public bool CurrentThreadIsLoopThread => Thread.CurrentThread == thread;

    public event Action? Signaled;

    public event Action? Timer;

    public long Now => clock.ElapsedMilliseconds;

    public void Signal() => Interlocked.Exchange(ref signaled, 1);

    public void UpdateTimer(long? dueTimeInMs)
    {
        lock (timerLock)
            dueTime = dueTimeInMs;
    }

    /// <summary>
    /// Runs the timers that are due and the jobs that are queued (game thread).
    /// </summary>
    public void Pump()
    {
        Debug.Assert(CurrentThreadIsLoopThread);

        bool timerDue;
        lock (timerLock)
        {
            timerDue = dueTime is { } due && Now >= due;
            if (timerDue)
                dueTime = null;
        }
        if (timerDue)
            Timer?.Invoke();

        for (var round = 0; round < MaxRoundsPerPump && Interlocked.Exchange(ref signaled, 0) == 1; round++)
            Signaled?.Invoke();
    }
}
