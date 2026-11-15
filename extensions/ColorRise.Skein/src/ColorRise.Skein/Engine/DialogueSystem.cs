using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Stride.Core;
using Stride.Core.Diagnostics;
using Stride.Core.Reflection;
using Stride.Core.Serialization.Contents;
using Stride.Games;

namespace ColorRise.Skein;

/// <summary>
/// What the dialogues of a game share: the commands and the functions of the game, the variables, and the language.
/// A game has one, made with the game; a script finds it in the services (<c>Services.GetService&lt;DialogueSystem&gt;()</c>).
/// </summary>
[GameSystem]
public sealed class DialogueSystem : GameSystemBase
{
    private static readonly Logger Log = GlobalLogger.GetLogger("ColorRise.Skein");
    private static readonly string SkeinName = typeof(DialogueSystem).Assembly.GetName().Name!;

    private readonly YarnActionRegistry actions = new();
    private readonly ConditionalWeakTable<YarnProject, TextLineProvider> providers = [];
    private readonly HashSet<Assembly> scanned = [];
    private volatile bool assembliesChanged = true;
    private string language = "";

    /// <summary>
    /// Makes the system of a game.
    /// </summary>
    public DialogueSystem(IServiceRegistry registry)
        : base(registry)
    {
        Variables = new VariableStorage(Log);
        AssemblyRegistry.AssemblyRegistered += OnAssemblyRegistered;
    }

    /// <summary>
    /// The language changed: the lines that follow are in the new one.
    /// </summary>
    public event Action<string>? LanguageChanged;

    /// <summary>
    /// The commands and the functions of the game: the methods that carry <see cref="YarnCommandAttribute"/> or
    /// <see cref="YarnFunctionAttribute"/> in the assemblies of the game, and what the game adds by hand.
    /// </summary>
    public YarnActionRegistry Actions
    {
        get
        {
            if (assembliesChanged)
            {
                assembliesChanged = false;
                RegisterAssemblies();
            }

            return actions;
        }
    }

    /// <summary>
    /// The variables of the game's story, shared by every runner that has none of its own. A game saves
    /// <see cref="VariableStorage.Save"/> with its save file, and loads it back.
    /// </summary>
    public VariableStorage Variables { get; }

    /// <summary>
    /// The language the lines are shown in, as the projects name their languages (<c>fr</c>, <c>pt-BR</c>). Empty, as
    /// at the start: each project's base language. A language a project lacks shows that project in its base language.
    /// </summary>
    public string Language
    {
        get => language;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (language == value)
                return;

            language = value;
            foreach (var (project, provider) in providers)
            {
                provider.Language = LanguageOf(project);
            }

            LanguageChanged?.Invoke(value);
        }
    }

    /// <summary>
    /// The lines of a project in the current language, from the string tables built with the project. It follows
    /// <see cref="Language"/>.
    /// </summary>
    public TextLineProvider GetLineProvider(YarnProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (!providers.TryGetValue(project, out var provider))
        {
            var content = Services.GetService<IContentManager>();
            string? url = null;
            if (content is ContentManager manager)
                manager.TryGetAssetUrl(project, out url);

            provider = new TextLineProvider(tableLanguage => LoadTable(content, url, tableLanguage), project.BaseLanguage, Log)
            {
                LineMetadata = project.LineMetadata,
                ShadowLines = project.ShadowLines,
                Language = LanguageOf(project),
            };
            providers.Add(project, provider);
        }

        return provider;
    }

    /// <summary>
    /// The system of a game, made now when the game did not make one (a host that creates no declared game system).
    /// </summary>
    internal static DialogueSystem Of(IServiceRegistry services)
    {
        var system = services.GetService<DialogueSystem>();
        if (system is null)
        {
            system = new DialogueSystem(services);
            services.AddService(system);
        }

        return system;
    }

    /// <inheritdoc />
    protected override void Destroy()
    {
        AssemblyRegistry.AssemblyRegistered -= OnAssemblyRegistered;
        base.Destroy();
    }

    private static YarnStringTable? LoadTable(IContentManager? content, string? projectUrl, string tableLanguage)
    {
        if (content is null || projectUrl is null)
            return null;

        var url = YarnProject.StringTableUrl(projectUrl, tableLanguage);
        return content.Exists(url) ? content.Load<YarnStringTable>(url) : null;
    }

    private string LanguageOf(YarnProject project) => language.Length > 0 ? language : project.BaseLanguage;

    // Raised from whatever thread loads an assembly: the game's thread registers at its next use of the actions
    private void OnAssemblyRegistered(object? sender, AssemblyRegisteredEventArgs e) => assembliesChanged = true;

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "A trimmed game keeps the methods that carry the attributes, as Skein's guide says.")]
    private void RegisterAssemblies()
    {
        // In a fixed order, whatever the order the assemblies registered in
        foreach (var assembly in AssemblyRegistry.FindAll().OrderBy(a => a.FullName, StringComparer.Ordinal))
        {
            // Only an assembly that references Skein can carry its attributes
            if (!scanned.Add(assembly) || !assembly.GetReferencedAssemblies().Any(r => r.Name == SkeinName))
                continue;

            try
            {
                actions.RegisterAssembly(assembly);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                Log.Error($"The commands and the functions of '{assembly.GetName().Name}' are not all registered: {exception.Message}");
            }
        }
    }
}
