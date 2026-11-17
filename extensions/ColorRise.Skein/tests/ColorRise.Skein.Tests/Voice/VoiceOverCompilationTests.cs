using ColorRise.Skein.Assets;
using ColorRise.Skein.Assets.Voice;
using ColorRise.Skein.Tests.Assets;
using Stride.Audio.Assets;
using Stride.Core.Assets;
using Stride.Core.Assets.Compiler;
using Stride.Core.BuildEngine;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Reflection;
using Stride.Core.Serialization.Contents;

namespace ColorRise.Skein.Tests.Voice;

/// <summary>
/// The sounds of a Yarn project's voice-over folders are compiled with it, and its voice tables give their URLs.
/// </summary>
[Collection(BuildCollection.Name)]
public sealed class VoiceOverCompilationTests : IDisposable
{
    private const string Act1 = "title: Start\n---\nOdile: The post is late. #line:post\nFisher: Mind the nets. #line:nets\n===\n";

    private readonly string buildPath = Path.Combine(Path.GetTempPath(), "SkeinVoice-" + Path.GetRandomFileName());
    private readonly Package package;
    private readonly AssetItem project;

    static VoiceOverCompilationTests()
    {
        AssemblyRegistry.Register(typeof(YarnProject).Assembly, AssemblyCommonCategories.Assets);
        AssemblyRegistry.Register(typeof(YarnProjectAsset).Assembly, AssemblyCommonCategories.Assets);
        AssemblyRegistry.Register(typeof(SoundAsset).Assembly, AssemblyCommonCategories.Assets);
        AssemblyRegistry.Register(typeof(Stride.Audio.Sound).Assembly, AssemblyCommonCategories.Assets);
        PackageSessionPublicHelper.FindAndSetMSBuildVersion();
    }

    public VoiceOverCompilationTests()
    {
        package = new Package { FullPath = new UFile(Path.Combine(buildPath, "Game", "Game.sdpkg")) };
        _ = new PackageSession(package);
        project = Add("Dialogue/Story", new YarnProjectAssetFactory().New());
        package.RootAssets.Add(new AssetReference(project.Id, project.Location));
        Add("Dialogue/Act1", new YarnScriptAsset { Text = Act1 });
    }

    public void Dispose()
    {
        if (Directory.Exists(buildPath))
            Directory.Delete(buildPath, recursive: true);
    }

    private YarnProjectAsset Project => (YarnProjectAsset)project.Asset;

    private AssetItem Add(string location, Asset asset)
    {
        var item = new AssetItem(location, asset);
        package.Assets.Add(item);
        return item;
    }

    /// <summary>
    /// A sound asset at a location, from a short tone written beside it.
    /// </summary>
    private AssetItem AddSound(string location)
    {
        var file = Path.Combine(buildPath, "Game", "Assets", location + ".wav");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, Tone.Wave(0.1, 440));
        return Add(location, new SoundAsset { Source = new UFile(file) });
    }

    private (ResultStatus Status, List<string> Log, Builder Builder) Build()
    {
        var prepared = new PackageCompiler(new RootPackageAssetEnumerator(package)).Prepare(new AssetCompilerContext { CompilationContext = typeof(AssetCompilationContext) });
        Assert.False(prepared.HasErrors, string.Join("\n", prepared.Messages));
        var log = new LoggerResult();
        Builder.OpenObjectDatabase(buildPath, "index");
        var builder = new Builder(log, buildPath, "index");
        builder.Root.Add(prepared.BuildSteps);
        builder.Run(Builder.Mode.Build, false);
        var command = Steps(prepared.BuildSteps).OfType<CommandBuildStep>().Single(s => s.Command.GetType().Name == "YarnProjectCommand");
        return (command.Status, [.. log.Messages.Where(m => m.Type >= LogMessageType.Warning).Select(m => $"{m.Type}: {m.Text}")], builder);
    }

    private static IEnumerable<BuildStep> Steps(BuildStep step) => step is ListBuildStep list ? list.Steps.SelectMany(Steps) : [step];

    [Fact]
    public void The_sounds_of_the_voice_folder_are_compiled_with_the_project_and_its_voice_table_gives_their_urls()
    {
        AddSound("Dialogue/Voice/en/post");

        var (status, log, builder) = Build();
        using (builder)
        {
            Assert.Equal(ResultStatus.Successful, status);
            Assert.Equal(["Warning: Dialogue/Story: in 'en', 1 of 2 lines have no voice-over clip in Dialogue/Voice/en."], log);

            MicrothreadLocalDatabases.MountDatabase([builder.Root.OutputObjects]);
            try
            {
                var content = new ContentManager(MicrothreadLocalDatabases.ProviderService);
                Assert.Equal(["en"], content.Load<YarnProject>("Dialogue/Story").VoicedLanguages);
                var voices = content.Load<YarnVoiceTable>(YarnProject.VoiceTableUrl("Dialogue/Story", "en"));
                Assert.Equal("en", voices.Language);
                Assert.Equal(new Dictionary<string, string> { ["line:post"] = "Dialogue/Voice/en/post" }, voices.Clips);

                // Nothing references the sound but the project: it is in the game for the project's sake
                Assert.True(content.Exists("Dialogue/Voice/en/post"));
            }
            finally
            {
                MicrothreadLocalDatabases.UnmountDatabase();
            }
        }

        var (same, _, builder2) = Build();
        builder2.Dispose();
        Assert.Equal(ResultStatus.NotTriggeredWasSuccessful, same);

        // A new clip builds the project again
        AddSound("Dialogue/Voice/en/nets");
        var (again, quiet, builder3) = Build();
        builder3.Dispose();
        Assert.Equal(ResultStatus.Successful, again);
        Assert.Empty(quiet);
    }

    [Fact]
    public void A_sound_named_after_no_line_and_two_clips_of_one_name_are_warnings()
    {
        AddSound("Dialogue/Voice/en/post");
        AddSound("Dialogue/Voice/en/Act1/post");
        AddSound("Dialogue/Voice/en/nest");
        AddSound("Dialogue/Voice/en/nets");

        var (status, log, builder) = Build();
        builder.Dispose();

        Assert.Equal(ResultStatus.Successful, status);
        Assert.Equal(
            [
                "Warning: Dialogue/Voice/en/Act1/post and Dialogue/Voice/en/post have the same name: the line 'line:post' speaks Dialogue/Voice/en/Act1/post in 'en'.",
                "Warning: Dialogue/Story: Dialogue/Voice/en/nest is the clip of no line, since no line has the id 'line:nest'. Name it after the id of its line.",
            ],
            log);
    }

    [Fact]
    public void Each_language_has_its_own_folder_and_an_empty_folder_setting_means_no_voice_over()
    {
        Project.Localisations.Add(new YarnLocalisation { Language = "fr" });
        AddSound("Dialogue/Voice/en/post");
        AddSound("Dialogue/Voice/fr/nets");
        AddSound("Dialogue/Music/theme");

        var found = YarnVoiceClips.Find(project);
        Assert.Equal(["en", "fr"], found.Languages.Select(l => l.Language));
        Assert.Equal(["Dialogue/Voice/en", "Dialogue/Voice/fr"], found.Languages.Select(l => l.Folder));
        Assert.Equal(["post"], found.Languages[0].Clips.Keys);
        Assert.Equal(["nets"], found.Languages[1].Clips.Keys);
        Assert.Equal(["Dialogue/Voice/en/post", "Dialogue/Voice/fr/nets"], new YarnProjectAssetCompiler().GetInputFiles(project).Select(u => u.Path).Order());

        Project.VoiceOverFolder = "Audio/{language}/Lines";
        Assert.All(YarnVoiceClips.Find(project).Languages, l => Assert.Empty(l.Clips));
        Assert.Equal("Dialogue/Audio/fr/Lines", YarnVoiceClips.FolderOf(project, "fr")!.FullPath);

        Project.VoiceOverFolder = "";
        Assert.Empty(YarnVoiceClips.Find(project).Languages);
        Assert.Empty(new YarnProjectAssetCompiler().GetInputFiles(project));
    }
}
