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
/// stored: game code reads it too, computed with the functions of <see cref="Functions"/>. Not thread-safe: a dialogue and
/// the game use it from the game's thread.
/// </remarks>
public sealed class VariableStorage : IVariableStorage
{
    /// <summary>
    /// How the names of the variables Yarn Spinner keeps for itself start. Some have no declaration in any script.
    /// </summary>
    public const string InternalPrefix = "$Yarn.Internal.";

    private readonly Dictionary<string, object> values = [];
    private readonly HashSet<string> undeclared = new(StringComparer.Ordinal);
    private readonly List<Program> programs = [];
    private readonly Dictionary<Program, SmartReader> readers = new(ReferenceEqualityComparer.Instance);
    private Func<YarnActionRegistry?>? functions;
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
    /// The program of the dialogue that reads this storage: Yarn Spinner sets it. Its variables are <see cref="Declare(Program)"/>d.
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
    /// The functions a smart variable calls when game code reads it, outside a dialogue (a dialogue has its own). The
    /// <see cref="DialogueSystem"/> gives the game's.
    /// </summary>
    public YarnActionRegistry? Functions
    {
        get => functions?.Invoke();
        set => functions = value is null ? null : () => value;
    }

    /// <summary>
    /// Gives the functions of a source that may change, read at each smart variable game code reads.
    /// </summary>
    internal void UseFunctions(Func<YarnActionRegistry?> source) => functions = source;

    /// <summary>
    /// The names of the variables that have been set, in no order.
    /// </summary>
    public IReadOnlyCollection<string> StoredNames => values.Keys;

    /// <summary>
    /// The names of the values that no project the storage knows declares: <see cref="Load"/> keeps them until a project
    /// that declares them is <see cref="Declare(Program)"/>d, which checks their types. <see cref="DropUndeclared"/>
    /// forgets them.
    /// </summary>
    public IReadOnlyCollection<string> UndeclaredNames => undeclared;

    /// <summary>
    /// Makes the variables of a project known: their defaults are read, their types checked when one is set, and
    /// <see cref="Load"/> keeps them. A storage serves every project that was declared to it.
    /// </summary>
    public void Declare(YarnProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        Declare(project.GetProgram());
    }

    /// <summary>
    /// Tells the storage the variables of a compiled program, as <see cref="Declare(YarnProject)"/> does for a project.
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
        CheckUndeclared(project);
    }

    /// <summary>
    /// Forgets the values that no project declares, with one warning that lists them: for a game that has declared all
    /// its projects, a save made with an older story keeps no variable the story lost.
    /// </summary>
    /// <returns>The names that were dropped, sorted.</returns>
    public IReadOnlyList<string> DropUndeclared()
    {
        var dropped = undeclared.Order(StringComparer.Ordinal).ToList();
        Drop(dropped, "the story does not declare");
        return dropped;
    }

    /// <inheritdoc />
    public bool TryGetValue<T>(string variableName, [NotNullWhen(true)] out T? result)
    {
        if (TryGetStored(variableName, program, out result, out var declared))
            return true;

        if (declared)
            return false;

        // Neither set nor declared with a value: a smart variable of the running dialogue, or nothing
        if (program is not null && SmartVariableEvaluator is not null && program.GetVariableKind(variableName) == VariableKind.Smart)
            return SmartVariableEvaluator.TryGetSmartVariable(variableName, out result);

        // Game code reads a smart variable: computed with the program that declares it
        if (SmartOwner(variableName) is { } owner)
        {
            if (!readers.TryGetValue(owner, out var reader))
                readers[owner] = reader = new SmartReader(this, owner);
            return reader.TryRead(variableName, Functions, out result);
        }

        return false;
    }

    /// <inheritdoc />
    public VariableKind GetVariableKind(string name) => GetVariableKind(name, program);

    /// <summary>
    /// Reads a variable for the dialogue of one project: its value, else the default that project declares (or another
    /// project of the storage).
    /// </summary>
    /// <param name="declared">Whether a value was set or a project declares a default: not a smart variable, then.</param>
    internal bool TryGetStored<T>(string variableName, Program? reader, [NotNullWhen(true)] out T? result, out bool declared)
    {
        if (values.TryGetValue(variableName, out var stored) && stored is T typed)
        {
            result = typed;
            declared = true;
            return true;
        }

        if (TryGetDefault(variableName, reader, out var initial))
        {
            declared = true;
            if (initial is T typedInitial)
            {
                result = typedInitial;
                return true;
            }

            result = default;
            return false;
        }

        declared = false;
        result = default;
        return false;
    }

    internal VariableKind GetVariableKind(string name, Program? reader)
    {
        if (values.ContainsKey(name))
            return VariableKind.Stored;

        if (reader is not null && reader.GetVariableKind(name) is var kind and not VariableKind.Unknown)
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
        undeclared.Clear();
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
    /// Replaces the variables by the ones of a snapshot. What a project the storage knows declares with another type, or
    /// as a smart variable, is dropped, with one warning that lists them: a save made with an older story loads. What no
    /// project it knows declares is kept, unchecked, until a project that declares it is <see cref="Declare(Program)"/>d
    /// (see <see cref="UndeclaredNames"/>): a save can be loaded before the scenes that use some of the projects start.
    /// </summary>
    /// <returns>The names that were dropped, sorted.</returns>
    public IReadOnlyList<string> Load(VariableSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var loaded = new Dictionary<string, object>();
        var unknown = new List<string>();
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
        undeclared.Clear();
        foreach (var (name, value) in loaded)
        {
            values[name] = value;
        }

        undeclared.UnionWith(unknown);
        dropped.Sort(StringComparer.Ordinal);
        if (dropped.Count > 0)
            log?.Warning($"The saved variables hold {dropped.Count} that the story declares otherwise; they were dropped: {string.Join(", ", dropped)}.");

        foreach (var name in changed)
        {
            RaiseChanged(name);
        }

        return dropped;

        void Take(string name, object value)
        {
            var verdict = value is null || loaded.ContainsKey(name) ? Verdict.Refused : Check(name, value, out _);
            if (verdict == Verdict.Refused)
            {
                dropped.Add(name);
                return;
            }

            loaded[name] = value!;
            if (verdict == Verdict.Undeclared)
                unknown.Add(name);
        }
    }

    private void Set(string name, object value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        // A storage that knows no project takes any name, and checks it once a project is declared
        var verdict = Check(name, value, out var reason);
        if (verdict == Verdict.Refused || (verdict == Verdict.Undeclared && programs.Count > 0))
            throw new ArgumentException($"The variable '{name}' cannot be set to {Describe(value)}: {reason}.", nameof(name));

        if (verdict == Verdict.Accepted)
            undeclared.Remove(name);
        else
            undeclared.Add(name);

        if (values.TryGetValue(name, out var current) && current.Equals(value))
            return;

        values[name] = value;
        VariableChanged?.Invoke(name, value);
    }

    /// <summary>
    /// Checks the values no project declared against a project that was just declared: the ones it declares with their
    /// type are kept, the others it declares are dropped.
    /// </summary>
    private void CheckUndeclared(Program project)
    {
        if (undeclared.Count == 0)
            return;

        var dropped = new List<string>();
        foreach (var name in undeclared.Order(StringComparer.Ordinal).ToList())
        {
            if (project.InitialValues.TryGetValue(name, out var initial))
            {
                if (Unwrap(initial).GetType() == values[name].GetType())
                    undeclared.Remove(name);
                else
                    dropped.Add(name);
            }
            else if (project.GetVariableKind(name) == VariableKind.Smart)
            {
                dropped.Add(name);
            }
        }

        Drop(dropped, "the story declares otherwise");
    }

    private void Drop(List<string> names, string why)
    {
        if (names.Count == 0)
            return;

        foreach (var name in names)
        {
            values.Remove(name);
            undeclared.Remove(name);
        }

        log?.Warning($"The variables hold {names.Count} that {why}; they were dropped: {string.Join(", ", names)}.");
        foreach (var name in names)
        {
            RaiseChanged(name);
        }
    }

    /// <summary>
    /// The program that declares a smart variable: the running dialogue's, else the first known one.
    /// </summary>
    private Program? SmartOwner(string name)
    {
        if (program is not null && program.GetVariableKind(name) == VariableKind.Smart)
            return program;

        foreach (var known in programs)
        {
            if (known.GetVariableKind(name) == VariableKind.Smart)
                return known;
        }

        return null;
    }

    private enum Verdict
    {
        /// <summary>A known project declares it with the value's type, or Yarn Spinner keeps it for itself.</summary>
        Accepted,

        /// <summary>No known project declares it.</summary>
        Undeclared,

        /// <summary>A known project declares it with another type, or as a smart variable.</summary>
        Refused,
    }

    /// <summary>
    /// Whether a value can be stored under a name: a known project must declare it as a stored variable of the value's
    /// type, but what Yarn Spinner keeps for itself needs no declaration.
    /// </summary>
    private Verdict Check(string name, object value, out string? reason)
    {
        reason = null;
        foreach (var known in programs)
        {
            if (known.InitialValues.TryGetValue(name, out var initial))
            {
                var declared = Unwrap(initial);
                if (declared.GetType() == value.GetType())
                    return Verdict.Accepted;

                reason = $"the story declares it as {Describe(declared)}";
                return Verdict.Refused;
            }
        }

        if (name.StartsWith(InternalPrefix, StringComparison.Ordinal))
            return Verdict.Accepted;

        foreach (var known in programs)
        {
            if (known.GetVariableKind(name) == VariableKind.Smart)
            {
                reason = "it is a smart variable, computed from its declaration";
                return Verdict.Refused;
            }
        }

        reason = "no script declares it";
        return Verdict.Undeclared;
    }

    private bool TryGetDefault(string name, Program? reader, [NotNullWhen(true)] out object? value)
    {
        if (reader is not null && reader.InitialValues.TryGetValue(name, out var initial))
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
            TryGetDefault(name, program, out value);
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

    /// <summary>
    /// Computes the smart variables of one program for game code: a Yarn Spinner dialogue that never runs, on the
    /// variables of the storage.
    /// </summary>
    private sealed class SmartReader : IVariableStorage, IYarnFunctionSource
    {
        private readonly VariableStorage storage;
        private readonly Program owner;
        private readonly Dialogue dialogue;

        public SmartReader(VariableStorage storage, Program owner)
        {
            this.storage = storage;
            this.owner = owner;
            dialogue = new YarnResponder(this, owner, events: null, this).Dialogue;
            SmartVariableEvaluator = dialogue;
        }

        public Program? Program { get; set; }

        public ISmartVariableEvaluator? SmartVariableEvaluator { get; set; }

        public bool TryRead<T>(string name, YarnActionRegistry? functions, [NotNullWhen(true)] out T? result)
        {
            Functions = functions;
            try
            {
                return dialogue.TryGetSmartVariable(name, out result);
            }
            finally
            {
                Functions = null;
            }
        }

        bool IYarnFunctionSource.CanCall(string name) => Functions?.HasFunction(name) == true;

        ValueTask<IConvertible> IYarnFunctionSource.InvokeAsync(string name, IConvertible[] arguments, CancellationToken cancellation)
        {
            // Yarn Spinner computes smart variables without waiting
            var answer = Functions!.InvokeFunctionAsync(name, arguments, cancellation);
            return answer.IsCompleted ? answer : new ValueTask<IConvertible>(Functions.DefaultResultOf(name));
        }

        /// <summary>
        /// The functions of the game while a smart variable is read.
        /// </summary>
        private YarnActionRegistry? Functions { get; set; }

        public bool TryGetValue<T>(string variableName, [NotNullWhen(true)] out T? result)
        {
            if (storage.TryGetStored(variableName, owner, out result, out var declared))
                return true;

            if (!declared && SmartVariableEvaluator is not null && owner.GetVariableKind(variableName) == VariableKind.Smart)
                return SmartVariableEvaluator.TryGetSmartVariable(variableName, out result);

            return false;
        }

        public VariableKind GetVariableKind(string name) => storage.GetVariableKind(name, owner);

        public void SetValue(string variableName, string stringValue) => storage.SetValue(variableName, stringValue);

        public void SetValue(string variableName, float floatValue) => storage.SetValue(variableName, floatValue);

        public void SetValue(string variableName, bool boolValue) => storage.SetValue(variableName, boolValue);

        public void Clear() => storage.Clear();
    }
}
