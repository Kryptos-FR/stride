using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Assets.Compiler;
using Stride.Core.BuildEngine;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Serialization.Contents;
using Stride.Core.Storage;

namespace ColorRise.Tessera.Tests.Assets;

/// <summary>
/// A package in a new temporary folder, whose assets compile into a database as the build of a game does, and the content
/// of that database.
/// </summary>
/// <remarks>
/// The build engine works in the folder of the application's data, which is the whole process's: a build points it at this
/// package's build folder while it runs, then back. The content is read through a mount of the build folder of its own.
/// </remarks>
internal sealed class AssetBuild : IDisposable
{
    private readonly Package package;
    private readonly string mount = "/tessera-" + Guid.NewGuid().ToString("N");
    private readonly IVirtualFileProvider mountProvider;
    private ObjectDatabase? database;

    static AssetBuild()
    {
        // A package session needs MSBuild, as in the engine's own compiler tests
        PackageSessionPublicHelper.FindAndSetMSBuildVersion();
    }

    public AssetBuild()
    {
        Folder = Path.Combine(Path.GetTempPath(), "TesseraBuild-" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(BuildFolder, "db"));
        mountProvider = VirtualFileSystem.MountFileSystem(mount, BuildFolder);

        // The package is never written; the compilers only need its paths to be absolute
        package = new Package { FullPath = new UFile(Path.Combine(Folder, "Game", "Game.sdpkg")) };
        _ = new PackageSession(package);
    }

    public string Folder { get; }

    private string BuildFolder => Path.Combine(Folder, "Build");

    /// <summary>
    /// The content manager of the build's database, made at the first use after each build.
    /// </summary>
    public ContentManager Content
    {
        get => field ??= new ContentManager(Files);
        private set;
    }

    // The database is opened at the first use after each build, to read the index the build wrote
    private DatabaseFileProviderService Files
    {
        get
        {
            if (field is null)
            {
                database = new ObjectDatabase(mount + "/db", VirtualFileSystem.ApplicationDatabaseIndexName, null, false);
                field = new DatabaseFileProviderService(new DatabaseFileProvider(database));
            }
            return field;
        }
        set;
    }

    /// <summary>
    /// Adds an asset at a URL, such as <c>Maps/Town</c>, as a root asset of the package: the build compiles it.
    /// </summary>
    public AssetItem Add(string url, Asset asset)
    {
        var item = new AssetItem(url, asset);
        package.Assets.Add(item);
        package.RootAssets.Add(new AssetReference(item.Id, item.Location));
        return item;
    }

    /// <summary>
    /// Writes a source file beside the assets.
    /// </summary>
    /// <returns>The file's path.</returns>
    public string WriteFile(string name, byte[] bytes)
    {
        var path = Path.Combine(Folder, "Game", "Assets", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>
    /// Compiles the package, into the same database at each call.
    /// </summary>
    public BuildResult Build()
    {
        CloseContent();
        var prepared = new PackageCompiler(new RootPackageAssetEnumerator(package)).Prepare(new AssetCompilerContext { CompilationContext = typeof(AssetCompilationContext) });
        var log = new LoggerResult();
        try
        {
            Builder.OpenObjectDatabase(BuildFolder, VirtualFileSystem.ApplicationDatabaseIndexName);
            using var builder = new Builder(log, BuildFolder, VirtualFileSystem.ApplicationDatabaseIndexName);
            builder.Root.Add(prepared.BuildSteps);
            builder.Run(Builder.Mode.Build, false);
            // What the asset compiler does after a build: the URLs of the content, which a content manager reads
            builder.WriteIndexFile(false);
        }
        finally
        {
            // Back to where the application's data was: the folder Stride gives it at start
            ((FileSystemProvider)VirtualFileSystem.ApplicationData).ChangeBasePath(
                Path.Combine(PlatformFolders.ApplicationDataDirectory, PlatformFolders.ApplicationDataSubDirectory));
        }

        var messages = prepared.Messages.Concat(log.Messages).ToList();
        var commands = Steps(prepared.BuildSteps).OfType<CommandBuildStep>()
            .Select(step => (Url: (step.Command as AssetCommand)?.Url ?? step.Command.Title, step.Status))
            .ToList();
        var errors = messages.Where(m => m.Type >= LogMessageType.Error).Select(m => m.Text).ToList();
        var succeeded = !prepared.HasErrors && errors.Count == 0
            && commands.All(c => c.Status is ResultStatus.Successful or ResultStatus.NotTriggeredWasSuccessful);
        return new BuildResult(succeeded, errors, [.. messages.Where(m => m.Type == LogMessageType.Warning).Select(m => m.Text)], commands);
    }

    /// <summary>
    /// Writes content at a URL through a content manager of its own, as another program would.
    /// </summary>
    public void Save(string url, object content) => new ContentManager(Files).Save(url, content);

    /// <summary>
    /// Reads content through a content manager of its own: a new object each time, read from the database.
    /// </summary>
    public T Load<T>(string url) where T : class => new ContentManager(Files).Load<T>(url);

    public void Dispose()
    {
        CloseContent();
        VirtualFileSystem.UnregisterProvider(mountProvider);
        if (Directory.Exists(Folder))
        {
            Directory.Delete(Folder, recursive: true);
        }
    }

    private void CloseContent()
    {
        database?.Dispose();
        database = null;
        Files = null!;
        Content = null!;
    }

    private static IEnumerable<BuildStep> Steps(BuildStep step) => step is ListBuildStep list ? list.Steps.SelectMany(Steps) : [step];
}

/// <summary>
/// What a build did: whether it succeeded, the texts of its errors and warnings, and the status of each command, by URL.
/// </summary>
internal sealed record BuildResult(bool Succeeded, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings,
    IReadOnlyList<(string Url, ResultStatus Status)> Commands);

/// <summary>
/// The tests that build assets, one after the other and while no other test runs: the build engine and the content they
/// read share the application's data folder and the build's database, which are the process's.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BuildCollection
{
    public const string Name = "Build";
}
