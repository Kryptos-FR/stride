// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

/// <summary>
/// The entry point the .NET host calls before the game's <c>Main</c> when <c>DOTNET_STARTUP_HOOKS</c> names this assembly.
/// </summary>
/// <remarks>
/// The class has no namespace and the method is <c>public static void Initialize()</c>: the host requires both. This
/// assembly references no Stride assembly, so that it can install the resolver of the agent's dependencies before any
/// agent code (and its generated module initializer) runs.
/// </remarks>
internal static class StartupHook
{
    /// <summary>Called by the .NET host before <c>Main</c>.</summary>
    public static void Initialize()
    {
        var hookTimestamp = Stopwatch.GetTimestamp();
        var pipeName = Environment.GetEnvironmentVariable("STRIDE_PLAY_PIPE");
        var mode = Environment.GetEnvironmentVariable("STRIDE_PLAY_AGENT_MODE");
        if (string.IsNullOrEmpty(pipeName) && mode != "meter")
            return;

        // The game's deps.json does not list the agent's own dependencies (the design-time assemblies used by the
        // YAML snapshots): resolve them from the agent's folder. Assemblies the game has win, since this handler runs
        // only when the default probing fails.
        var agentDirectory = Path.GetDirectoryName(typeof(StartupHook).Assembly.Location)!;
        var resolved = new List<string>();
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var candidate = Path.Combine(agentDirectory, name.Name + ".dll");
            if (!File.Exists(candidate))
                return null;
            lock (resolved)
                resolved.Add($"{name.Name} ({new FileInfo(candidate).Length / 1024} KB)");
            return context.LoadFromAssemblyPath(candidate);
        };
        AppContext.SetData("Stride.PlayMode.ResolvedFromAgent", resolved);
        AppContext.SetData("Stride.PlayMode.HookTimestamp", hookTimestamp);

        var agent = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(agentDirectory, "Stride.PlayMode.Spike.Agent.dll"));
        agent.GetType("Stride.PlayMode.Spike.AgentBootstrap", throwOnError: true)!
            .GetMethod("Start", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [pipeName, mode]);
    }
}
