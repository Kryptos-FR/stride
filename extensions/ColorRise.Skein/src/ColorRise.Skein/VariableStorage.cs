using System.Diagnostics.CodeAnalysis;
using Stride.Core.Diagnostics;
using Yarn;

namespace ColorRise.Skein;

/// <summary>
/// Where the variables of the scripts live: Yarn Spinner reads and sets them here, a game reads, sets and listens to them,
/// and saves them as a <see cref="VariableSnapshot"/>. How often a node was visited and what a <c>once</c> remembers are
/// variables too, so they are saved with the rest.
/// </summary>
/// <remarks>
/// A variable that was never set reads the default its script declares. A smart variable is computed each time and never
/// stored. Not thread-safe: a dialogue and the game use it from the game's thread.
/// </remarks>
public sealed class VariableStorage : IVariableStorage
{
    /// <summary>
    /// How the names of the variables Yarn Spinner keeps for itself start. Some have no declaration in any script.
    /// </summary>
    public const string InternalPrefix = "$Yarn.Internal.";

    private readonly Dictionary<string, object> values = [];
    private readonly List<Program> programs = [];
    private readonly ILogger? log;
    private Program? program;

    /// <summary>
    /// Makes an empty storage.
    /// </summary>
    /// <param name="log">Where <see cref="Load"/> says what it dropped.</param>
    public VariableStorage(ILogger? log = null)
    {
        this.log = log;
    }

    /// <summary>
    /// A variable has another value: its name, and the value it reads now (the default again after a <see cref="Clear"/>
    /// or a <see cref="Load"/> that doesn't hold it; null when no project declares it any more).
    /// </summary>
    public event Action<string, object?>? VariableChanged;

    /// <summary>
    /// The program of the dialogue that reads this storage: Yarn Spinner sets it. Its variables are <see cref="Declare"/>d.
    /// </summary>
    public Program? Program
    {
        get => program;
        set
        {
            program = value;
            if (value is not null)
                Declare(value);
        }
    }

    /// <summary>
    /// What computes the smart variables: Yarn Spinner sets it.
    /// </summary>
    public ISmartVariableEvaluator? SmartVariableEvaluator { get; set; }

    /// <summary>
    /// The names of the variables that have been set, in no order.
    /// </summary>
    public IReadOnlyCollection<string> StoredNames => values.Keys;

    /// <summary>
    /// Makes the variables of a project known: their defaults are read, their types checked when one is set, and
    /// <see cref="Load"/> keeps them. A storage serves every project that was declared to it.
    /// </summary>
    public void Declare(Program project)
    {
        ArgumentNullException.ThrowIfNull(project);
        foreach (var known in programs)
        {
            if (ReferenceEquals(known, project))
                return;
        }

        programs.Add(project);
    }

    /// <inheritdoc />
    public bool TryGetValue<T>(string variableName, [NotNullWhen(true)] out T? result)
    {
        if (values.TryGetValue(variableName, out var stored) && stored is T typed)
        {
            result = typed;
            return true;
        }

        if (TryGetDefault(variableName, out var initial))
        {
            if (initial is T typedInitial)
            {
                result = typedInitial;
                return true;
            }

            result = default;
            return false;
        }

        // Neither set nor declared with a value: a smart variable of the running dialogue, or nothing
        if (program is not null && SmartVariableEvaluator is not null && program.GetVariableKind(variableName) == VariableKind.Smart)
            return SmartVariableEvaluator.TryGetSmartVariable(variableName, out result);

        result = default;
        return false;
    }

    /// <inheritdoc />
    public VariableKind GetVariableKind(string name)
    {
        if (values.ContainsKey(name))
            return VariableKind.Stored;

        if (program is not null && program.GetVariableKind(name) is var kind and not VariableKind.Unknown)
            return kind;

        foreach (var known in programs)
        {
            if (known.GetVariableKind(name) is var other and not VariableKind.Unknown)
                return other;
        }

        return VariableKind.Unknown;
    }

    /// <inheritdoc />
    public void SetValue(string variableName, string stringValue) => Set(variableName, stringValue);

    /// <inheritdoc />
    public void SetValue(string variableName, float floatValue) => Set(variableName, floatValue);

    /// <inheritdoc />
    public void SetValue(string variableName, bool boolValue) => Set(variableName, boolValue);

    /// <summary>
    /// Forgets every value: each variable reads its default again.
    /// </summary>
    public void Clear()
    {
        var names = values.Keys.ToList();
        values.Clear();
        foreach (var name in names)
        {
            RaiseChanged(name);
        }
    }

    /// <summary>
    /// Copies the variables that have been set. Save between dialogues: a dialogue that is running is not part of it.
    /// </summary>
    public VariableSnapshot Save()
    {
        var snapshot = new VariableSnapshot();
        foreach (var (name, value) in values)
        {
            switch (value)
            {
                case float number:
                    snapshot.Numbers[name] = number;
                    break;
                case string text:
                    snapshot.Strings[name] = text;
                    break;
                case bool flag:
                    snapshot.Bools[name] = flag;
                    break;
            }
        }

        return snapshot;
    }

    /// <summary>
    /// Replaces the variables by the ones of a snapshot. When the storage knows a project, what no project declares any
    /// more, what a project now declares with another type and what is now a smart variable are dropped, with one warning
    /// that lists them: a save made with an older story loads.
    /// </summary>
    /// <returns>The names that were dropped, sorted.</returns>
    public IReadOnlyList<string> Load(VariableSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var loaded = new Dictionary<string, object>();
        var dropped = new List<string>();
        foreach (var (name, value) in snapshot.Numbers)
        {
            Take(name, value);
        }

        foreach (var (name, value) in snapshot.Strings)
        {
            Take(name, value);
        }

        foreach (var (name, value) in snapshot.Bools)
        {
            Take(name, value);
        }

        var changed = new List<string>();
        foreach (var name in values.Keys)
        {
            if (!loaded.ContainsKey(name))
                changed.Add(name);
        }

        foreach (var (name, value) in loaded)
        {
            if (!values.TryGetValue(name, out var current) || !current.Equals(value))
                changed.Add(name);
        }

        values.Clear();
        foreach (var (name, value) in loaded)
        {
            values[name] = value;
        }

        dropped.Sort(StringComparer.Ordinal);
        if (dropped.Count > 0)
            log?.Warning($"The saved variables hold {dropped.Count} that the story no longer has; they were dropped: {string.Join(", ", dropped)}.");

        foreach (var name in changed)
        {
            RaiseChanged(name);
        }

        return dropped;

        void Take(string name, object value)
        {
            if (value is null || loaded.ContainsKey(name) || !Accepts(name, value, out _))
                dropped.Add(name);
            else
                loaded[name] = value;
        }
    }

    private void Set(string name, object value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        if (!Accepts(name, value, out var reason))
            throw new ArgumentException($"The variable '{name}' cannot be set to {Describe(value)}: {reason}.", nameof(name));

        if (values.TryGetValue(name, out var current) && current.Equals(value))
            return;

        values[name] = value;
        VariableChanged?.Invoke(name, value);
    }

    /// <summary>
    /// Whether a value can be stored under a name: always when no project is known, or for what Yarn Spinner keeps for
    /// itself without a declaration; else the name must be a stored variable of a project, of the value's type.
    /// </summary>
    private bool Accepts(string name, object value, [NotNullWhen(false)] out string? reason)
    {
        reason = null;
        if (programs.Count == 0)
            return true;

        foreach (var known in programs)
        {
            if (known.InitialValues.TryGetValue(name, out var initial))
            {
                var declared = Unwrap(initial);
                if (declared.GetType() == value.GetType())
                    return true;

                reason = $"the story declares it as {Describe(declared)}";
                return false;
            }

        }

        if (name.StartsWith(InternalPrefix, StringComparison.Ordinal))
            return true;

        foreach (var known in programs)
        {
            if (known.GetVariableKind(name) == VariableKind.Smart)
            {
                reason = "it is a smart variable, computed from its declaration";
                return false;
            }
        }

        reason = "no script declares it";
        return false;
    }

    private bool TryGetDefault(string name, [NotNullWhen(true)] out object? value)
    {
        if (program is not null && program.InitialValues.TryGetValue(name, out var initial))
        {
            value = Unwrap(initial);
            return true;
        }

        foreach (var known in programs)
        {
            if (known.InitialValues.TryGetValue(name, out initial))
            {
                value = Unwrap(initial);
                return true;
            }
        }

        value = null;
        return false;
    }

    private void RaiseChanged(string name)
    {
        if (VariableChanged is not { } changed)
            return;

        if (!values.TryGetValue(name, out var value))
            TryGetDefault(name, out value);
        changed(name, value);
    }

    private static object Unwrap(Operand operand) => operand.ValueCase switch
    {
        Operand.ValueOneofCase.StringValue => operand.StringValue,
        Operand.ValueOneofCase.BoolValue => operand.BoolValue,
        _ => operand.FloatValue,
    };

    private static string Describe(object value) => value switch
    {
        float => "a number",
        string => "a string",
        _ => "a boolean",
    };
}
