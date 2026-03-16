using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace StrideAssets.VisualStudio
{
    /// <summary>
    /// AsyncPackage that generates pkgdef entries for TextMate grammar registration
    /// and bootstraps the asset index on solution open.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid("d4b5c6a7-8e9f-4a1b-b2c3-d4e5f6a7b8c9")]
    [ProvidePackageCodeBase("StrideAssets.VisualStudio.dll")]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideTextMateRepository("StrideAssets", @"$PackageFolder$\TextMate")]
    [ProvideTextMateContentTypeMapping("stride-asset", "source.stride-asset")]
    public sealed class StrideAssetsPackage : AsyncPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            Log.Initialize(this);

            var solution = await GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
            string? solutionDir = null;
            if (solution != null)
                solution.GetSolutionInfo(out solutionDir, out _, out _);

            if (!string.IsNullOrEmpty(solutionDir))
            {
                // JoinableTaskFactory.RunAsync tracks the task for VS shutdown coordination
                // and surfaces exceptions to VS error reporting instead of swallowing them.
                JoinableTaskFactory.RunAsync(async () =>
                {
                    await WorkspaceScanner.Instance.StartAsync(solutionDir!);
                }).FileAndForget("stride/workspace-scan");
            }

            // TODO Phase 1+: register IVsSolutionEvents to restart scanner when solution changes
        }
    }

    /// <summary>
    /// Writes a CodeBase entry to the Packages registry key so the CLR can find
    /// the unsigned assembly during package load (no GAC, no VSSDK auto-codebase).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class ProvidePackageCodeBaseAttribute : RegistrationAttribute
    {
        private readonly string _fileName;

        public ProvidePackageCodeBaseAttribute(string fileName) => _fileName = fileName;

        public override void Register(RegistrationContext context)
        {
            using var key = context.CreateKey($@"Packages\{context.ComponentType.GUID:B}");
            key.SetValue("CodeBase", $@"$PackageFolder$\{_fileName}");
        }

        public override void Unregister(RegistrationContext context) { }
    }

    /// <summary>
    /// Registers a TextMate grammar repository directory in the VS registry.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    internal sealed class ProvideTextMateRepositoryAttribute : RegistrationAttribute
    {
        private readonly string _id;
        private readonly string _path;

        public ProvideTextMateRepositoryAttribute(string id, string path)
        {
            _id = id;
            _path = path;
        }

        public override void Register(RegistrationContext context)
        {
            using var key = context.CreateKey(@"TextMate\Repositories");
            key.SetValue(_id, _path);
        }

        public override void Unregister(RegistrationContext context)
            => context.RemoveKey(@"TextMate\Repositories");
    }

    /// <summary>
    /// Maps a VS content type to a TextMate grammar scope name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    internal sealed class ProvideTextMateContentTypeMappingAttribute : RegistrationAttribute
    {
        private readonly string _contentType;
        private readonly string _scopeName;

        public ProvideTextMateContentTypeMappingAttribute(string contentType, string scopeName)
        {
            _contentType = contentType;
            _scopeName = scopeName;
        }

        public override void Register(RegistrationContext context)
        {
            using var key = context.CreateKey(@"TextMate\LanguageMapping\ContentTypeMapping");
            key.SetValue(_contentType, _scopeName);
        }

        public override void Unregister(RegistrationContext context)
            => context.RemoveKey(@"TextMate\LanguageMapping\ContentTypeMapping");
    }
}
