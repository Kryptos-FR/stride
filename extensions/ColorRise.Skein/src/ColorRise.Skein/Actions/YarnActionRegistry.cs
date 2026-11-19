using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace ColorRise.Skein;

/// <summary>
/// The commands and the functions a game gives to its scripts: found by their attributes in the game's assemblies, or
/// added by hand. A dialogue runs the commands through it and takes its functions.
/// </summary>
/// <remarks>
/// Not thread-safe: a game registers and runs from its own thread.
/// </remarks>
public sealed class YarnActionRegistry
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private readonly Dictionary<string, Command> commands = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Function> functions = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, Converter> converters = [];

    /// <summary>
    /// What finds the objects commands name. Without it, only static commands and commands added by hand run.
    /// </summary>
    public IYarnTargetResolver? Targets { get; set; }

    /// <summary>
    /// Adds a command. The handler's parameters are the words of the command, as for <see cref="YarnCommandAttribute"/>;
    /// a handler bound to an object runs on that object, so the command names no target.
    /// </summary>
    /// <exception cref="ArgumentException">The name is not one word, or the handler returns something else than nothing or a task.</exception>
    /// <exception cref="InvalidOperationException">Another handler has the name.</exception>
    public void AddCommandHandler(string name, Delegate handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        AddCommand(new Command(CheckName(name, "command"), handler.Method, handler.Target, TargetType: null, Documentation: null));
    }

    /// <summary>
    /// Removes a command.
    /// </summary>
    /// <returns>Whether there was one.</returns>
    public bool RemoveCommandHandler(string name) => commands.Remove(name);

    /// <summary>
    /// Adds a function: it takes and returns booleans, numbers and strings.
    /// </summary>
    /// <exception cref="ArgumentException">The name is not one word, or a type of the function is not one scripts have.</exception>
    /// <exception cref="InvalidOperationException">Another function has the name.</exception>
    public void AddFunction(string name, Delegate function)
    {
        ArgumentNullException.ThrowIfNull(function);
        AddFunction(new Function(CheckName(name, "function"), function, Documentation: null));
    }

    /// <summary>
    /// Removes a function.
    /// </summary>
    /// <returns>Whether there was one.</returns>
    public bool RemoveFunction(string name) => functions.Remove(name);

    /// <summary>
    /// Adds the converter of a type: the parameters of that type take the words of commands through it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The type has a converter.</exception>
    public void AddConverter<T>(YarnConvert<T> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        AddConverter(new Converter(typeof(T), text => converter(text, out var value) ? (true, value) : (false, null), converter.Method));
    }

    /// <summary>
    /// Removes the converter of a type.
    /// </summary>
    /// <returns>Whether there was one.</returns>
    public bool RemoveConverter(Type type) => converters.Remove(type);

    /// <summary>
    /// Adds the methods of an assembly that carry <see cref="YarnCommandAttribute"/>, <see cref="YarnFunctionAttribute"/> or <see cref="YarnConverterAttribute"/>.
    /// Adding an assembly again changes nothing.
    /// </summary>
    /// <exception cref="ArgumentException">A method cannot be a command or a function; the message names it.</exception>
    /// <exception cref="InvalidOperationException">Two methods have one name; the message names both.</exception>
    [RequiresUnreferencedCode("Commands and functions are found by reflection: a trimmed game must keep the methods that carry the attributes.")]
    public void RegisterAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var type in TypesOf(assembly))
        {
            RegisterType(type);
        }
    }

    /// <summary>
    /// Adds the methods of an assembly as <see cref="RegisterAssembly"/> does, but goes on after a method that cannot be
    /// a command or a function: the others are added.
    /// </summary>
    /// <returns>What is wrong with each method that was not added; the message names it.</returns>
    [RequiresUnreferencedCode("Commands and functions are found by reflection: a trimmed game must keep the methods that carry the attributes.")]
    public IReadOnlyList<string> RegisterWhatFits(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var problems = new List<string>();
        foreach (var type in TypesOf(assembly))
        {
            if (type.IsGenericTypeDefinition)
                continue;

            foreach (var method in type.GetMethods(Declared))
            {
                try
                {
                    RegisterMethod(type, method);
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
                {
                    problems.Add(exception.Message);
                }
            }
        }

        return problems;
    }

    /// <summary>
    /// Adds the methods of a type that carry <see cref="YarnCommandAttribute"/>, <see cref="YarnFunctionAttribute"/> or <see cref="YarnConverterAttribute"/>.
    /// Adding a type again changes nothing.
    /// </summary>
    /// <exception cref="ArgumentException">A method cannot be a command or a function; the message names it.</exception>
    /// <exception cref="InvalidOperationException">Two methods have one name; the message names both.</exception>
    public void RegisterType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type.IsGenericTypeDefinition)
            return;

        foreach (var method in type.GetMethods(Declared))
        {
            RegisterMethod(type, method);
        }
    }

    private static IEnumerable<Type> TypesOf(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            // What the assembly could load still gives its commands
            return exception.Types.OfType<Type>();
        }
    }

    private void RegisterMethod(Type type, MethodInfo method)
    {
        if (method.GetCustomAttribute<YarnConverterAttribute>() is { } converter
            && !(converters.TryGetValue(converter.Type, out var sameConverter) && sameConverter.Method == method))
        {
            var parameters = method.GetParameters();
            if (!method.IsStatic || method.IsGenericMethodDefinition || method.ReturnType != typeof(bool) || parameters.Length != 2
                || parameters[0].ParameterType != typeof(string) || !parameters[1].IsOut || parameters[1].ParameterType != converter.Type.MakeByRefType())
                throw new ArgumentException($"The converter of {converter.Type.Name} ({Name(method)}) must be 'static bool {method.Name}(string text, out {converter.Type.Name} value)'.");

            AddConverter(new Converter(converter.Type, text =>
            {
                object?[] arguments = [text, null];
                try
                {
                    return method.Invoke(null, arguments) is true ? (true, arguments[1]) : (false, null);
                }
                catch (TargetInvocationException exception) when (exception.InnerException is not null)
                {
                    ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                    throw;
                }
            }, method));
        }

        if (method.GetCustomAttribute<YarnCommandAttribute>() is { } command
            && !(commands.TryGetValue(command.Name, out var sameCommand) && sameCommand.Method == method))
        {
            AddCommand(new Command(CheckName(command.Name, "command"), method, Target: null, method.IsStatic ? null : type, command.Description));
        }

        if (method.GetCustomAttribute<YarnFunctionAttribute>() is { } function
            && !(functions.TryGetValue(function.Name, out var sameFunction) && sameFunction.Delegate.Method == method))
        {
            var name = CheckName(function.Name, "function");
            if (!method.IsStatic)
                throw new ArgumentException($"The function '{name}' ({Name(method)}) must be a static method: a function has no object to run on.");
            if (method.IsGenericMethodDefinition)
                throw new ArgumentException($"The function '{name}' ({Name(method)}) cannot be a generic method.");

            var signature = method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType).ToArray();
            if (method.ReturnType == typeof(void) || signature.Any(t => t.IsByRef || t.IsPointer))
                throw new ArgumentException($"The function '{name}' ({Name(method)}) must return a boolean, a number or a string, and take its parameters by value.");

            AddFunction(new Function(name, method.CreateDelegate(Expression.GetFuncType(signature)), function.Description));
        }
    }

    /// <summary>
    /// Runs a command: finds its handler by the first word, its target and its parameters from the other words, and
    /// waits for the handler when it returns a task.
    /// </summary>
    /// <param name="commandText">The text of the command, the values of the script already in it.</param>
    /// <param name="cancellation">Given to the handler when it takes one: the dialogue stops waiting when it is cancelled.</param>
    /// <returns>False when no handler has the command's name: the command is someone else's to handle.</returns>
    /// <exception cref="YarnCommandException">The target was not found, or the words do not fit the parameters.</exception>
    public ValueTask<bool> RunCommandAsync(string commandText, CancellationToken cancellation) => RunCommandAsync(commandText, targets: null, cancellation);

    /// <summary>
    /// Runs a command as <see cref="RunCommandAsync(string, CancellationToken)"/> does, finding the objects it names
    /// the way of this run: a dialogue of one scene finds them in its scene.
    /// </summary>
    /// <param name="commandText">The text of the command, the values of the script already in it.</param>
    /// <param name="targets">What finds the objects the command names; null for <see cref="Targets"/>.</param>
    /// <param name="cancellation">Given to the handler when it takes one: the dialogue stops waiting when it is cancelled.</param>
    /// <returns>False when no handler has the command's name: the command is someone else's to handle.</returns>
    /// <exception cref="YarnCommandException">The target was not found, or the words do not fit the parameters.</exception>
    public ValueTask<bool> RunCommandAsync(string commandText, IYarnTargetResolver? targets, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(commandText);
        targets ??= Targets;

        var words = YarnCommandText.Split(commandText);
        if (words.Count == 0 || !commands.TryGetValue(words[0], out var command))
            return new ValueTask<bool>(false);

        var next = 1;
        var target = command.Target;
        if (command.TargetType is { } targetType)
        {
            if (words.Count < 2)
                throw new YarnCommandException(commandText, $"it runs on a {targetType.Name}: the first word after '{command.Name}' names the target.");

            target = Find(commandText, targets, words[next++], targetType, "the target");
        }

        var arguments = Bind(commandText, command, words, next, targets, cancellation);

        object? result;
        try
        {
            result = command.Method.Invoke(target, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            // What the handler throws comes out as it threw it
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }

        return result switch
        {
            Task task => Wait(task),
            ValueTask task => Wait(task.AsTask()),
            _ => new ValueTask<bool>(true),
        };

        static async ValueTask<bool> Wait(Task task)
        {
            await task.ConfigureAwait(true);
            return true;
        }
    }

    /// <summary>
    /// Describes every command and function, sorted by name: what the compiler needs to check the calls of the functions,
    /// and what editors show.
    /// </summary>
    public IReadOnlyList<YarnActionDescription> Describe()
    {
        var descriptions = new List<YarnActionDescription>();
        foreach (var command in commands.Values)
        {
            descriptions.Add(new YarnActionDescription
            {
                Name = command.Name,
                Kind = YarnActionKind.Command,
                Parameters = [.. command.Method.GetParameters().Where(p => p.ParameterType != typeof(CancellationToken)).Select(DescribeParameter)],
                TargetType = command.TargetType?.Name,
                IsAsync = typeof(Task).IsAssignableFrom(command.Method.ReturnType) || command.Method.ReturnType == typeof(ValueTask),
                Documentation = command.Documentation,
                Method = Name(command.Method),
            });
        }

        foreach (var function in functions.Values)
        {
            descriptions.Add(new YarnActionDescription
            {
                Name = function.Name,
                Kind = YarnActionKind.Function,
                Parameters = [.. function.Delegate.Method.GetParameters().Select(DescribeParameter)],
                ReturnType = YarnType(function.Delegate.Method.ReturnType),
                Documentation = function.Documentation,
                Method = Name(function.Delegate.Method),
            });
        }

        descriptions.Sort((a, b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : string.CompareOrdinal(a.Name, b.Name));
        return descriptions;
    }

    /// <summary>
    /// Whether a function has this name.
    /// </summary>
    public bool HasFunction(string name) => functions.ContainsKey(name);

    /// <summary>
    /// Calls a function with values of a script.
    /// </summary>
    /// <param name="name">The name of the function.</param>
    /// <param name="arguments">The values of the script: booleans, numbers and strings, converted to the function's parameters.</param>
    /// <param name="cancellation">Not used by functions yet.</param>
    /// <returns>A boolean, a number (a float) or a string.</returns>
    /// <exception cref="ArgumentException">No function has the name, or the arguments do not fit its parameters.</exception>
    public ValueTask<IConvertible> InvokeFunctionAsync(string name, IReadOnlyList<IConvertible> arguments, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(arguments);
        if (!functions.TryGetValue(name, out var function))
            throw new ArgumentException($"No function is named '{name}'.", nameof(name));

        var parameters = function.Delegate.Method.GetParameters();
        var values = new object?[parameters.Length];
        var next = 0;
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (i == parameters.Length - 1 && IsParams(parameter))
            {
                var elementType = parameter.ParameterType.GetElementType()!;
                var rest = Array.CreateInstance(elementType, Math.Max(arguments.Count - next, 0));
                for (var j = 0; next < arguments.Count; j++)
                {
                    rest.SetValue(ToParameter(function, arguments[next++], elementType), j);
                }

                values[i] = rest;
            }
            else if (next < arguments.Count)
            {
                values[i] = ToParameter(function, arguments[next++], parameter.ParameterType);
            }
            else if (parameter.HasDefaultValue)
            {
                values[i] = parameter.DefaultValue;
            }
            else
            {
                throw new ArgumentException($"The function '{name}' takes {parameters.Length} parameter{(parameters.Length == 1 ? "" : "s")}, and was given {arguments.Count}.");
            }
        }

        if (next < arguments.Count)
            throw new ArgumentException($"The function '{name}' takes {parameters.Length} parameter{(parameters.Length == 1 ? "" : "s")}, and was given {arguments.Count}.");

        object? result;
        try
        {
            result = function.Delegate.DynamicInvoke(values);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            // What the function throws comes out as it threw it
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }

        return new ValueTask<IConvertible>(ToScript(function, result));
    }

    private static object ToParameter(Function function, IConvertible argument, Type type)
    {
        try
        {
            return System.Convert.ChangeType(argument, Nullable.GetUnderlyingType(type) ?? type, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or InvalidCastException)
        {
            throw new ArgumentException($"The function '{function.Name}' was given '{argument}', which is not a {type.Name}.", exception);
        }
    }

    private static IConvertible ToScript(Function function, object? result) => result switch
    {
        bool or string => (IConvertible)result,
        IConvertible number when IsNumber(number.GetType()) => number.ToSingle(CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException($"The function '{function.Name}' ({Name(function.Delegate.Method)}) returned {(result is null ? "null" : $"a {result.GetType().Name}")}: a function returns a boolean, a number or a string."),
    };

    private void AddCommand(Command command)
    {
        var method = command.Method;
        if (method.IsGenericMethodDefinition)
            throw new ArgumentException($"The command '{command.Name}' ({Name(method)}) cannot be a generic method.");

        var returns = method.ReturnType;
        if (returns != typeof(void) && returns != typeof(ValueTask) && !typeof(Task).IsAssignableFrom(returns))
            throw new ArgumentException($"The command '{command.Name}' ({Name(method)}) returns a {returns.Name}: a command returns nothing, or a task the dialogue waits for.");

        if (method.GetParameters().Any(p => p.ParameterType.IsByRef || p.ParameterType.IsPointer))
            throw new ArgumentException($"The command '{command.Name}' ({Name(method)}) must take its parameters by value.");

        if (commands.TryGetValue(command.Name, out var existing))
            throw new InvalidOperationException($"Two commands are named '{command.Name}': {Name(existing.Method)} and {Name(method)}.");

        commands.Add(command.Name, command);
    }

    private void AddConverter(Converter converter)
    {
        if (converters.TryGetValue(converter.Type, out var existing))
            throw new InvalidOperationException($"Two converters make a {converter.Type.Name}: {Name(existing.Method)} and {Name(converter.Method)}.");

        converters.Add(converter.Type, converter);
    }

    private void AddFunction(Function function)
    {
        var method = function.Delegate.Method;
        if (YarnType(method.ReturnType) is null)
            throw new ArgumentException($"The function '{function.Name}' ({Name(method)}) returns a {method.ReturnType.Name}: a function returns a boolean, a number or a string.");

        var parameters = method.GetParameters();
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var type = i == parameters.Length - 1 && IsParams(parameter) ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
            if (YarnType(type) is null)
                throw new ArgumentException($"The parameter '{parameter.Name}' of the function '{function.Name}' ({Name(method)}) is a {type.Name}: a function takes booleans, numbers and strings.");
        }

        if (functions.TryGetValue(function.Name, out var existing))
            throw new InvalidOperationException($"Two functions are named '{function.Name}': {Name(existing.Delegate.Method)} and {Name(method)}.");

        functions.Add(function.Name, function);
    }

    private object?[] Bind(string commandText, Command command, IReadOnlyList<string> words, int next, IYarnTargetResolver? targets, CancellationToken cancellation)
    {
        var parameters = command.Method.GetParameters();
        var arguments = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (parameter.ParameterType == typeof(CancellationToken))
            {
                arguments[i] = cancellation;
            }
            else if (i == parameters.Length - 1 && IsParams(parameter))
            {
                var elementType = parameter.ParameterType.GetElementType()!;
                var rest = Array.CreateInstance(elementType, words.Count - next);
                for (var j = 0; next < words.Count; j++)
                {
                    rest.SetValue(Convert(commandText, targets, words[next++], elementType, parameter), j);
                }

                arguments[i] = rest;
            }
            else if (next < words.Count)
            {
                arguments[i] = Convert(commandText, targets, words[next++], parameter.ParameterType, parameter);
            }
            else if (parameter.HasDefaultValue)
            {
                arguments[i] = parameter.DefaultValue;
            }
            else
            {
                throw new YarnCommandException(commandText, $"the parameter '{parameter.Name}' ({DescribeParameter(parameter).Type}) is missing.");
            }
        }

        if (next < words.Count)
        {
            var count = parameters.Count(p => p.ParameterType != typeof(CancellationToken));
            throw new YarnCommandException(commandText, $"'{words[next]}' is one word too many: '{command.Name}' takes {count} parameter{(count == 1 ? "" : "s")}.");
        }

        return arguments;
    }

    private object? Convert(string commandText, IYarnTargetResolver? targets, string word, Type type, ParameterInfo parameter)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (converters.TryGetValue(type, out var converter))
        {
            return converter.Convert(word) is (true, var value)
                ? value
                : throw new YarnCommandException(commandText, $"'{word}' is not a {type.Name} for the parameter '{parameter.Name}'.");
        }

        if (type == typeof(string))
            return word;

        if (type == typeof(bool))
        {
            if (bool.TryParse(word, out var flag))
                return flag;

            // <<walk Odile running>>: the name of a boolean parameter says true
            if (string.Equals(word, parameter.Name, StringComparison.OrdinalIgnoreCase))
                return true;

            throw new YarnCommandException(commandText, $"'{word}' is not a boolean for the parameter '{parameter.Name}': write true, false or {parameter.Name}.");
        }

        if (type.IsEnum)
        {
            if (!int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) && Enum.TryParse(type, word, ignoreCase: true, out var value))
                return value;

            // <<paint {$colour}>>: a Yarn enum gives the number of its case, which is a case of the C# enum of the same values
            if (float.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number == MathF.Round(number)
                && Enum.ToObject(type, (long)number) is var numbered && Enum.IsDefined(type, numbered))
                return numbered;

            throw new YarnCommandException(commandText, $"'{word}' is not a {type.Name} for the parameter '{parameter.Name}': write one of {string.Join(", ", Enum.GetNames(type))}.");
        }

        if (IsNumber(type))
        {
            try
            {
                return System.Convert.ChangeType(word, type, CultureInfo.InvariantCulture);
            }
            catch (Exception exception) when (exception is FormatException or OverflowException or InvalidCastException)
            {
                throw new YarnCommandException(commandText, $"'{word}' is not a number for the parameter '{parameter.Name}' ({type.Name}).");
            }
        }

        return Find(commandText, targets, word, type, $"the parameter '{parameter.Name}'");
    }

    private static object Find(string commandText, IYarnTargetResolver? targets, string name, Type type, string role)
    {
        if (targets is null)
            throw new YarnCommandException(commandText, $"nothing finds objects by name here, so {role} '{name}' ({type.Name}) cannot be found.");

        return targets.Find(name, type)
            ?? throw new YarnCommandException(commandText, $"no {type.Name} named '{name}' was found for {role}.");
    }

    private YarnActionParameter DescribeParameter(ParameterInfo parameter)
    {
        var isParams = IsParams(parameter);
        var type = isParams ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
        type = Nullable.GetUnderlyingType(type) ?? type;
        var converted = converters.ContainsKey(type);
        return new YarnActionParameter
        {
            Name = parameter.Name ?? "",
            Type = converted || type.IsEnum ? "string" : YarnType(type) ?? "any",
            TypeName = YarnType(type) is null ? type.Name : null,
            IsOptional = parameter.HasDefaultValue || isParams,
            DefaultValue = parameter.HasDefaultValue ? DefaultText(parameter.DefaultValue) : null,
            IsParams = isParams,
            Choices = type.IsEnum && !converted ? Enum.GetNames(type) : [],
        };
    }

    private static string? DefaultText(object? value) => value switch
    {
        null => null,
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    private static string? YarnType(Type type)
    {
        if (type == typeof(string))
            return "string";
        if (type == typeof(bool))
            return "bool";
        return IsNumber(type) ? "number" : null;
    }

    private static bool IsNumber(Type type) => Type.GetTypeCode(type) is >= TypeCode.SByte and <= TypeCode.Decimal && !type.IsEnum;

    private static bool IsParams(ParameterInfo parameter) => parameter.ParameterType.IsArray && parameter.IsDefined(typeof(ParamArrayAttribute), inherit: false);

    private static string CheckName(string name, string kind)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0 || name.Any(c => char.IsWhiteSpace(c) || c is '"' or '<' or '>' or '{' or '}'))
            throw new ArgumentException($"'{name}' cannot name a {kind}: a name is one word.", nameof(name));

        return name;
    }

    private static string Name(MethodInfo method) => $"{method.DeclaringType?.Name}.{method.Name}";

    private sealed record Command(string Name, MethodInfo Method, object? Target, Type? TargetType, string? Documentation);

    private sealed record Function(string Name, Delegate Delegate, string? Documentation);

    private sealed record Converter(Type Type, Func<string, (bool Converted, object? Value)> Convert, MethodInfo Method);
}
