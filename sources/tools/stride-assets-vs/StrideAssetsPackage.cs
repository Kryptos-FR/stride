using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace StrideAssets.VisualStudio
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideOptionPage(typeof(StrideOptionsPage), "Stride", "Asset Navigator", 0, 0, true)]
    [ProvideTextMateRepository("StrideAssets", @"$PackageFolder$\TextMate")]
    [ProvideTextMateContentTypeMapping("stride-asset", "source.stride-asset")]
    public sealed class StrideAssetsPackage : AsyncPackage
    {
        public const string PackageGuidString = "d4b5c6a7-8e9f-4a1b-b2c3-d4e5f6a7b8c9";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Registers a TextMate grammar repository directory in the VS registry.
    /// Generates a pkgdef entry: [$RootKey$\TextMate\Repositories] "id"="path"
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
        {
            context.RemoveKey(@"TextMate\Repositories");
        }
    }

    /// <summary>
    /// Maps a VS content type to a TextMate grammar scope name.
    /// Generates a pkgdef entry: [$RootKey$\TextMate\LanguageMapping\ContentTypeMapping] "contentType"="scopeName"
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
        {
            context.RemoveKey(@"TextMate\LanguageMapping\ContentTypeMapping");
        }
    }
}
