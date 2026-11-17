// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Stride.Core.Diagnostics;
using Stride.Engine;
using Stride.Games;
using Stride.PlayMode.Protocol;
using Stride.PlayMode.Snapshots;

namespace Stride.PlayMode.Agent;

/// <summary>
/// The play agent: a game system that runs last in each update and serves its subscribers the tree of the running scenes,
/// snapshots of the entities they choose, the game's log and a heartbeat.
/// </summary>
/// <remarks>
/// <para>
/// Everything that touches the game runs on the game thread, in <see cref="Update"/>; the connections read and write on
/// tasks of their own, and a full queue drops a message instead of blocking the game. Several subscribers can be connected,
/// each with its own entities and rate.
/// </para>
/// <para>
/// With no subscriber, the agent follows nothing and costs a check per frame.
/// </para>
/// </remarks>
public sealed class PlayAgentSystem : GameSystemBase
{
    /// <summary>The highest snapshot rate a subscriber can ask for.</summary>
    public const double MaxRateHz = 60;

    private const int PollPeriodFrames = 30;
    private const int MaxLogsPerFrame = 64;
    private const int MaxQueuedLogs = 1024;

    private readonly Game game;
    private readonly ConcurrentQueue<Stream> newStreams = new();
    private readonly List<PlaySubscriber> subscribers = [];
    private readonly PlayTreeTracker tracker = new();
    private readonly ConcurrentQueue<PlayLog> logs = new();
    private readonly ConcurrentQueue<Assembly> loadedAssemblies = new();
    private readonly HashSet<string> sentAssemblies = [];
    private readonly Dictionary<int, byte[]> frameSnapshots = [];
    private readonly Stopwatch heartbeatClock = new();
    private long frame;
    private int queuedLogs;
    private long lastPollFrame;
    private long heartbeatFrame;
    private double agentTicks;
    private double agentMaxTicks;
    private int periodFrames;
    private int periodSnapshots;
    private double snapshotTicks;
    private double snapshotMaxTicks;
    private long lostSubscriberFrames;
    private bool prepared;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayAgentSystem"/> class. Add it to the game's systems.
    /// </summary>
    /// <param name="game">The game it serves.</param>
    public PlayAgentSystem(Game game)
        : base(game?.Services ?? throw new ArgumentNullException(nameof(game)))
    {
        this.game = game;
        Enabled = true;
        // After the scripts and the scene system: a snapshot sees the state at the end of the frame's update
        UpdateOrder = int.MaxValue;
        GlobalLogger.GlobalMessageLogged += OnMessageLogged;
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        game.Exiting += OnGameExiting;
    }

    /// <summary>
    /// The number of connected subscribers, as of the last frame.
    /// </summary>
    public int SubscriberCount => subscribers.Count;

    /// <summary>
    /// The tree tracker, on the game thread.
    /// </summary>
    public PlayTreeTracker Tracker => tracker;

    /// <summary>
    /// Adds a subscriber connected on this stream (a pipe, a socket); it gets its hello and the tree on the next frame. Can
    /// be called from any thread.
    /// </summary>
    /// <param name="stream">A connected stream; the agent owns it.</param>
    public void AddSubscriber(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        newStreams.Enqueue(stream);
    }

    /// <inheritdoc/>
    public override void Update(GameTime gameTime)
    {
        frame++;
        if (subscribers.Count == 0 && newStreams.IsEmpty)
        {
            // Nobody listens: follow nothing, so that nothing piles up
            if (tracker.SceneInstance is not null)
                tracker.Update(null);
            DropLogs();
            return;
        }

        var start = Stopwatch.GetTimestamp();
        RemoveClosedSubscribers();
        var joined = AddNewSubscribers();
        HandleCommands();

        var replaced = tracker.Update(game.SceneSystem.SceneInstance);
        if (frame - lastPollFrame >= PollPeriodFrames)
        {
            lastPollFrame = frame;
            tracker.Poll();
        }
        if (replaced)
        {
            foreach (var subscriber in subscribers)
                subscriber.NeedsTree = true;
        }
        else if (tracker.TakeBatch(frame) is { } batch)
        {
            // The subscribers that join now get the tree, which already has these changes
            Broadcast(batch, x => !x.NeedsTree);
        }
        SendTrees();
        if (joined && !prepared)
            PrepareSnapshots();

        SendAssemblies();
        SendLogs();
        TakeSnapshots(gameTime);

        var elapsed = Stopwatch.GetTimestamp() - start;
        agentTicks += elapsed;
        agentMaxTicks = Math.Max(agentMaxTicks, elapsed);
        periodFrames++;
        SendHeartbeat(gameTime);
    }

    /// <inheritdoc/>
    protected override void Destroy()
    {
        GlobalLogger.GlobalMessageLogged -= OnMessageLogged;
        AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
        game.Exiting -= OnGameExiting;
        tracker.Dispose();
        foreach (var subscriber in subscribers)
            subscriber.Connection.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(1));
        subscribers.Clear();
        base.Destroy();
    }

    private void RemoveClosedSubscribers()
    {
        for (var i = subscribers.Count - 1; i >= 0; i--)
        {
            var subscriber = subscribers[i];
            if (!subscriber.Connection.Completion.IsCompleted)
                continue;
            subscribers.RemoveAt(i);
            lostSubscriberFrames += subscriber.Connection.DroppedFrames;
            _ = subscriber.Connection.DisposeAsync().AsTask();
        }
    }

    private bool AddNewSubscribers()
    {
        var joined = false;
        while (newStreams.TryDequeue(out var stream))
        {
            var subscriber = new PlaySubscriber(new PlayConnection(stream));
            subscriber.Connection.Start(subscriber.Incoming.Enqueue);
            subscriber.Connection.Send(CreateHello());
            subscribers.Add(subscriber);
            joined = true;
        }
        return joined;
    }

    private void HandleCommands()
    {
        foreach (var subscriber in subscribers)
        {
            while (subscriber.Incoming.TryDequeue(out var message))
            {
                switch (message)
                {
                    case PlaySubscribe subscribe:
                        subscriber.Selection = subscribe.Handles;
                        subscriber.RateHz = Math.Clamp(subscribe.RateHz, 0, MaxRateHz);
                        subscriber.LastSnapshot = 0;
                        break;
                    case PlayQuit:
                        game.Exit();
                        break;
                }
            }
        }
    }

    private void SendTrees()
    {
        byte[]? tree = null;
        foreach (var subscriber in subscribers)
        {
            if (!subscriber.NeedsTree)
                continue;
            tree ??= tracker.BuildTree(frame).Encode();
            subscriber.Connection.SendFrame(tree);
            subscriber.NeedsTree = false;
        }
    }

    private void PrepareSnapshots()
    {
        prepared = true;
        // The first snapshot would create the serializers (5 to 15 ms): make them off the game thread now
        var types = tracker.Entities.Values.SelectMany(x => x.Components).Select(x => x.GetType()).Distinct().ToList();
        _ = Task.Run(() =>
        {
            try
            {
                PlayMirrorSerializer.Prepare(types);
            }
            catch (Exception e)
            {
                QueueLog(new PlayLog(frame, PlayLogLevel.Warning, "PlayAgent", $"Preparing the snapshots failed: {e.Message}", null));
            }
        });
    }

    private void TakeSnapshots(GameTime gameTime)
    {
        frameSnapshots.Clear();
        var now = Stopwatch.GetTimestamp();
        foreach (var subscriber in subscribers)
        {
            if (!subscriber.IsSnapshotDue(now))
                continue;
            subscriber.LastSnapshot = now;
            var start = Stopwatch.GetTimestamp();
            var encoded = new List<byte[]>(subscriber.Selection.Count);
            foreach (var handle in subscriber.Selection)
            {
                if (!frameSnapshots.TryGetValue(handle, out var bytes))
                {
                    if (!tracker.Entities.TryGetValue(handle, out var entity))
                        continue;
                    bytes = PlaySnapshot.EncodeEntity(PlaySnapshotWriter.Write(entity, tracker.GetHandle));
                    frameSnapshots.Add(handle, bytes);
                }
                encoded.Add(bytes);
            }
            subscriber.Connection.SendFrame(PlaySnapshot.EncodeFrame(frame, gameTime.Total.TotalSeconds, encoded));
            var elapsed = Stopwatch.GetTimestamp() - start;
            periodSnapshots++;
            snapshotTicks += elapsed;
            snapshotMaxTicks = Math.Max(snapshotMaxTicks, elapsed);
        }
    }

    private void SendHeartbeat(GameTime gameTime)
    {
        if (!heartbeatClock.IsRunning)
        {
            heartbeatClock.Start();
            heartbeatFrame = frame;
            return;
        }
        if (heartbeatClock.Elapsed.TotalSeconds < 1)
            return;

        var seconds = heartbeatClock.Elapsed.TotalSeconds;
        var toMicroseconds = 1_000_000.0 / Stopwatch.Frequency;
        var stats = new PlayAgentStats(
            periodFrames,
            periodFrames > 0 ? agentTicks / periodFrames * toMicroseconds : 0,
            agentMaxTicks * toMicroseconds,
            periodSnapshots,
            periodSnapshots > 0 ? snapshotTicks / periodSnapshots * toMicroseconds : 0,
            snapshotMaxTicks * toMicroseconds,
            subscribers.Sum(x => x.Connection.BytesQueued),
            lostSubscriberFrames + subscribers.Sum(x => x.Connection.DroppedFrames));
        Broadcast(new PlayHeartbeat(frame, gameTime.Total.TotalSeconds, (frame - heartbeatFrame) / seconds, stats));

        heartbeatClock.Restart();
        heartbeatFrame = frame;
        agentTicks = agentMaxTicks = snapshotTicks = snapshotMaxTicks = 0;
        periodFrames = periodSnapshots = 0;
    }

    private void Broadcast(PlayMessage message, Func<PlaySubscriber, bool>? filter = null)
    {
        var bytes = message.Encode();
        foreach (var subscriber in subscribers)
        {
            if (filter?.Invoke(subscriber) != false)
                subscriber.Connection.SendFrame(bytes);
        }
    }

    private PlayHello CreateHello()
    {
        var assemblies = new List<PlayAssemblyInfo>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (Describe(assembly) is { } info && sentAssemblies.Add(info.Name))
                assemblies.Add(info);
        }
        return new PlayHello(Environment.ProcessId, InformationalVersion(typeof(Game).Assembly), InformationalVersion(typeof(PlayAgentSystem).Assembly), game.GetType().FullName, assemblies);
    }

    private void SendAssemblies()
    {
        List<PlayAssemblyInfo>? assemblies = null;
        while (loadedAssemblies.TryDequeue(out var assembly))
        {
            if (Describe(assembly) is { } info && sentAssemblies.Add(info.Name))
                (assemblies ??= []).Add(info);
        }
        if (assemblies is not null)
            Broadcast(new PlayAssemblies(assemblies));
    }

    private void SendLogs()
    {
        for (var i = 0; i < MaxLogsPerFrame && logs.TryDequeue(out var log); i++)
        {
            Interlocked.Decrement(ref queuedLogs);
            Broadcast(log);
        }
    }

    private void DropLogs()
    {
        while (logs.TryDequeue(out _))
            Interlocked.Decrement(ref queuedLogs);
        loadedAssemblies.Clear();
    }

    private void QueueLog(PlayLog log)
    {
        // A game that logs every frame must not grow the queue without end
        if (Interlocked.Increment(ref queuedLogs) > MaxQueuedLogs)
        {
            Interlocked.Decrement(ref queuedLogs);
            return;
        }
        logs.Enqueue(log);
    }

    private void OnMessageLogged(ILogMessage message)
    {
        if (subscribers.Count == 0 && newStreams.IsEmpty)
            return;
        QueueLog(new PlayLog(Interlocked.Read(ref frame), (PlayLogLevel)message.Type, message.Module, message.Text ?? string.Empty, message.ExceptionInfo?.ToString()));
    }

    private void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs args) => loadedAssemblies.Enqueue(args.LoadedAssembly);

    private void OnGameExiting(object? sender, EventArgs e)
    {
        // The last frame's messages, then the goodbye; the editor knows the exit was the game's own
        var goodbye = new PlayGoodbye("The game is exiting.").Encode();
        foreach (var subscriber in subscribers)
        {
            subscriber.Connection.SendFrame(goodbye);
            subscriber.Connection.FlushAndCloseAsync(TimeSpan.FromMilliseconds(500)).Wait();
        }
    }

    private static PlayAssemblyInfo? Describe(Assembly assembly)
    {
        if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location))
            return null;
        // The .NET runtime's own assemblies are the same on both sides of a connection
        if (assembly.Location.StartsWith(RuntimeEnvironment.GetRuntimeDirectory(), StringComparison.OrdinalIgnoreCase))
            return null;
        var name = assembly.GetName();
        // The editor loads the game's code built for itself: the shape tells whether its types still read this build's
        var shape = PlayAssemblyShape.AppliesTo(assembly) ? PlayAssemblyShape.Compute(assembly) : Guid.Empty;
        return new PlayAssemblyInfo(name.Name ?? string.Empty, name.Version?.ToString(), assembly.ManifestModule.ModuleVersionId, shape);
    }

    private static string InformationalVersion(Assembly assembly)
        => assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? string.Empty;
}
