// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Stride.Core.Diagnostics;
using Stride.Core.MicroThreading;
using Stride.Engine;
using Stride.Engine.Processors;
using Stride.PlayMode.Protocol;

namespace Stride.PlayMode.Agent;

/// <summary>
/// A script's exception that the engine caught, as the watcher found it.
/// </summary>
/// <param name="Kind">Where it was thrown.</param>
/// <param name="Script">The script that threw; <c>null</c> when not found.</param>
/// <param name="ScriptType">The script's type, from the script or from the exception's stack; <c>null</c> when not known.</param>
/// <param name="Exception">The exception.</param>
internal sealed record PlayCaughtScriptException(PlayScriptExceptionKind Kind, ScriptComponent? Script, Type? ScriptType, Exception Exception)
{
    /// <summary>The script's entity; <c>null</c> when not known.</summary>
    public Entity? Entity => Script?.Entity;
}

/// <summary>
/// Turns a script's exception into a pause, for the play agent's "pause on script exception": while it watches, the script
/// system catches the scripts' exceptions instead of ending the game, and the watcher finds which script threw.
/// </summary>
/// <remarks>
/// <para>
/// The engine's script system already knows how to go on after a script's exception: the live scripting debugger turns off
/// its scheduler's <c>PropagateExceptions</c> (internal, set by reflection here). The script is then stopped (a sync script no
/// longer updates, an async script's micro-thread ends), the exception is logged, and the game goes on. Without it, the
/// exception ends the game.
/// </para>
/// <para>
/// The watcher reads the exceptions from two places, on the game thread: the script system's log (a sync script's
/// <c>Update</c> or <c>Start</c>, a <c>Cancel</c>), and the scheduler's end of a failed micro-thread (an async script). For a
/// sync script, the script that threw is the one the script system removed in that frame (its private set of scripts, read by
/// reflection); the exception's stack gives its type when that set cannot be read. What the script system does not run (a
/// handler of a window event, a game system) is not caught: it still ends the game.
/// </para>
/// </remarks>
internal sealed class PlayScriptExceptionWatcher : IDisposable
{
    private const string ScriptSystemModule = "ScriptSystem";

    private static readonly PropertyInfo? PropagateExceptionsProperty = typeof(Scheduler).GetProperty("PropagateExceptions", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    private static readonly FieldInfo? RegisteredScriptsField = typeof(ScriptSystem).GetField("registeredScripts", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? AsyncMicroThreadField = typeof(AsyncScript).GetField("MicroThread", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private readonly ScriptSystem scriptSystem;
    private readonly ConcurrentQueue<(Exception Exception, ScriptComponent[]? Registered)> logged = new();
    private readonly ConcurrentQueue<(MicroThread MicroThread, Exception Exception)> failedMicroThreads = new();
    private Scheduler? scheduler;
    private bool savedPropagate;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayScriptExceptionWatcher"/> class.
    /// </summary>
    /// <param name="scriptSystem">The game's script system.</param>
    public PlayScriptExceptionWatcher(ScriptSystem scriptSystem)
    {
        ArgumentNullException.ThrowIfNull(scriptSystem);
        this.scriptSystem = scriptSystem;
    }

    /// <summary>
    /// Whether the engine can be made to catch the scripts' exceptions (the internal members the watcher uses exist).
    /// </summary>
    public static bool IsSupported => PropagateExceptionsProperty is not null;

    /// <summary>
    /// Whether the watcher watches: the script system catches the scripts' exceptions.
    /// </summary>
    public bool IsWatching => scheduler is not null;

    /// <summary>
    /// Makes the script system catch the scripts' exceptions, and watches for them.
    /// </summary>
    /// <returns><c>false</c> when the engine cannot be made to (<see cref="IsSupported"/>).</returns>
    public bool Start()
    {
        if (IsWatching)
            return true;
        if (PropagateExceptionsProperty is null || scriptSystem.Scheduler is not { } watched)
            return false;
        scheduler = watched;
        savedPropagate = (bool)PropagateExceptionsProperty.GetValue(watched)!;
        PropagateExceptionsProperty.SetValue(watched, false);
        watched.MicroThreadEnded += OnMicroThreadEnded;
        GlobalLogger.GlobalMessageLogged += OnMessageLogged;
        return true;
    }

    /// <summary>
    /// Gives the script system back its way with exceptions (they end the game again), and stops watching.
    /// </summary>
    public void Stop()
    {
        if (scheduler is not { } watched)
            return;
        scheduler = null;
        GlobalLogger.GlobalMessageLogged -= OnMessageLogged;
        watched.MicroThreadEnded -= OnMicroThreadEnded;
        PropagateExceptionsProperty!.SetValue(watched, savedPropagate);
        logged.Clear();
        failedMicroThreads.Clear();
    }

    /// <inheritdoc/>
    public void Dispose() => Stop();

    /// <summary>
    /// The exceptions caught since the last call, with their scripts. Called on the game thread once the scripts ran (the
    /// agent's update, the last of each update).
    /// </summary>
    /// <param name="entities">The entities of the running scenes, to find an async script by its micro-thread when the script
    /// system's set of scripts cannot be read.</param>
    public List<PlayCaughtScriptException> TakeCaught(IEnumerable<Entity> entities)
    {
        var caught = new List<PlayCaughtScriptException>();
        if (logged.IsEmpty && failedMicroThreads.IsEmpty)
            return caught;

        var registered = Registered();
        while (logged.TryDequeue(out var item))
        {
            var stackType = FindScriptType(item.Exception);
            // The script that threw is the one the script system removed since (of the stack's type, when it says)
            var removed = item.Registered?.Where(x => registered?.Contains(x) == false).ToList() ?? [];
            var script = removed.FirstOrDefault(x => stackType is not null && stackType.IsInstanceOfType(x)) ?? (removed.Count == 1 ? removed[0] : null);
            caught.Add(new PlayCaughtScriptException(FindKind(item.Exception, script), script, script?.GetType() ?? stackType, item.Exception));
        }
        while (failedMicroThreads.TryDequeue(out var item))
        {
            var candidates = registered is not null ? registered : entities.SelectMany(x => x.Components).OfType<ScriptComponent>();
            var script = candidates.OfType<AsyncScript>().FirstOrDefault(x => ReferenceEquals(MicroThreadOf(x), item.MicroThread));
            caught.Add(new PlayCaughtScriptException(PlayScriptExceptionKind.Async, script, script?.GetType() ?? FindScriptType(item.Exception), item.Exception));
        }
        return caught;
    }

    /// <summary>
    /// The script type of the exception's stack: the first frame of a method of a script (or of a type nested in one, as an
    /// async method's state machine is).
    /// </summary>
    internal static Type? FindScriptType(Exception exception)
    {
        foreach (var frame in new StackTrace(exception, false).GetFrames())
        {
            for (var type = frame.GetMethod()?.DeclaringType; type is not null; type = type.DeclaringType)
            {
                if (typeof(ScriptComponent).IsAssignableFrom(type) && type.Assembly != typeof(ScriptComponent).Assembly)
                    return type;
            }
        }
        return null;
    }

    /// <summary>
    /// Where a script's exception that the script system logged was thrown, from the script system's frames of its stack.
    /// </summary>
    internal static PlayScriptExceptionKind FindKind(Exception exception, ScriptComponent? script)
    {
        foreach (var frame in new StackTrace(exception, false).GetFrames())
        {
            if (frame.GetMethod() is not { } method)
                continue;
            var type = method.DeclaringType;
            while (type?.DeclaringType is not null)
                type = type.DeclaringType;
            if (type != typeof(ScriptSystem))
                continue;
            // The sync scripts' updates run in a batch; a script's Start runs in a closure of the script system's update
            if (method.Name.Contains(nameof(ScriptSystem.Update), StringComparison.Ordinal) && method.DeclaringType != typeof(ScriptSystem))
                return PlayScriptExceptionKind.Start;
            if (method.Name == "ExecuteBatchOfSyncScripts")
                return PlayScriptExceptionKind.Update;
            return PlayScriptExceptionKind.Other;
        }
        return script is SyncScript ? PlayScriptExceptionKind.Update : PlayScriptExceptionKind.Other;
    }

    private HashSet<ScriptComponent>? Registered() => RegisteredScriptsField?.GetValue(scriptSystem) as HashSet<ScriptComponent>;

    private static MicroThread? MicroThreadOf(AsyncScript script) => AsyncMicroThreadField?.GetValue(script) as MicroThread;

    private void OnMessageLogged(ILogMessage message)
    {
        if (message is not LogMessage { Module: ScriptSystemModule, Exception: { } exception } || message.Type < LogMessageType.Error)
            return;
        // The script system logs before it removes the script, on the game thread: the scripts it has now, to find the one it
        // removes
        ScriptComponent[]? registered;
        try
        {
            registered = Registered()?.ToArray();
        }
        catch (InvalidOperationException)
        {
            // Logged from another thread while the set changed: the stack tells the script's type
            registered = null;
        }
        logged.Enqueue((exception, registered));
    }

    private void OnMicroThreadEnded(object? sender, SchedulerThreadEventArgs e)
    {
        if (e.MicroThread is { State: MicroThreadState.Failed, Exception: { } exception } microThread && exception is not OperationCanceledException)
            failedMicroThreads.Enqueue((microThread, exception));
    }
}
