using ColorRise.Saga.Expressions;

namespace ColorRise.Saga;

/// <summary>
/// A signal an action emitted, with its arguments, for the game to act on.
/// </summary>
public sealed record SagaSignal(string Name, IReadOnlyList<SagaValue> Arguments);
