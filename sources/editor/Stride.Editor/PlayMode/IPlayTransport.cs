// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using Stride.PlayMode.Protocol;

namespace Stride.Editor.PlayMode;

/// <summary>
/// The channel a played game's agent connects through: a named pipe now (<see cref="PipePlayTransport"/>), a socket to a
/// device later. The play session above it does not depend on which.
/// </summary>
[Experimental("STREXP012")]
public interface IPlayTransport : IAsyncDisposable
{
    /// <summary>
    /// The variables the game needs to find this transport (the pipe's name), added to its environment.
    /// </summary>
    IReadOnlyDictionary<string, string> GameEnvironment { get; }

    /// <summary>
    /// Waits for the game's agent to connect.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The connected stream; the caller owns it.</returns>
    Task<Stream> AcceptAsync(CancellationToken cancellationToken);
}

/// <summary>
/// A named pipe with a random name, which only the editor and the game it starts know. On Linux and macOS it is a Unix domain
/// socket. One game connects to it.
/// </summary>
[Experimental("STREXP012")]
public sealed class PipePlayTransport : IPlayTransport
{
    private readonly NamedPipeServerStream server;
    private int accepted;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipePlayTransport"/> class and opens its pipe.
    /// </summary>
    public PipePlayTransport()
    {
        PipeName = $"stride-play-{Guid.NewGuid():N}";
        server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        GameEnvironment = new Dictionary<string, string> { [PlayProtocol.PipeVariable] = PipeName };
    }

    /// <summary>
    /// The name of the pipe.
    /// </summary>
    public string PipeName { get; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> GameEnvironment { get; }

    /// <inheritdoc/>
    public async Task<Stream> AcceptAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref accepted, 1) != 0)
            throw new InvalidOperationException("The pipe accepts one game.");
        await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        return server;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => server.DisposeAsync();
}
