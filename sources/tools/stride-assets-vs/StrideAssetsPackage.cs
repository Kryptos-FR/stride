using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace StrideAssets.VisualStudio
{
    /// <summary>
    /// Minimal AsyncPackage whose sole purpose is to generate pkgdef entries
    /// for TextMate grammar registration. The Extensibility deployment doesn't
    /// process static pkgdef files, so we need a Package class to trigger
    /// pkgdef generation and VSSDK processing.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid("d4b5c6a7-8e9f-4a1b-b2c3-d4e5f6a7b8c9")]
    [ProvideTextMateRepository("StrideAssets", @"$PackageFolder$\TextMate")]
    [ProvideTextMateContentTypeMapping("stride-asset", "source.stride-asset")]
    public sealed class StrideAssetsPackage : AsyncPackage
    {
        protected override Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
            => Task.CompletedTask;
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
