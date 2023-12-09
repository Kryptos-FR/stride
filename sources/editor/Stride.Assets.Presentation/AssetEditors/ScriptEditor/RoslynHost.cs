// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.Reflection;
using RoslynPad.Editor;
using RoslynPad.Roslyn;

namespace Stride.Assets.Presentation.AssetEditors.ScriptEditor
{
    /// <summary>
    /// Manages services needed by Roslyn.
    /// </summary>
    public sealed class RoslynHost : RoslynPad.Roslyn.RoslynHost
    {
        private readonly RoslynWorkspace workspace;

        public RoslynHost()
            : base(AdditionalAssemblies())
        {
            // Create default workspace
            workspace = CreateWorkspace();
        }

        /// <summary>
        /// The roslyn workspace.
        /// </summary>
        public RoslynWorkspace Workspace => workspace;

        public override RoslynWorkspace CreateWorkspace()
        {
            return new(HostServices, roslynHost: this);
        }

        private static IEnumerable<Assembly> AdditionalAssemblies()
        {
            return new[]
            {
                //Assembly.Load("Microsoft.CodeAnalysis.Workspaces"),
                //Assembly.Load("Microsoft.CodeAnalysis.CSharp.Workspaces"),
                //Assembly.Load("Microsoft.CodeAnalysis.Features"),
                //Assembly.Load("Microsoft.CodeAnalysis.CSharp.Features"),
                Assembly.Load("Microsoft.CodeAnalysis.Workspaces.MSBuild"),
                //typeof(IRoslynHost).Assembly, // RoslynPad.Roslyn
                typeof(SymbolDisplayPartExtensions).Assembly, // RoslynPad.Roslyn.Windows
                typeof(AvalonEditTextContainer).Assembly, // RoslynPad.Editor.Windows
            };
        }
    }

    ///// <summary>
    ///// Manages services needed by Roslyn.
    ///// </summary>
    //internal sealed class RoslynHostOld : IRoslynHost
    //{
    //    private readonly RoslynWorkspace workspace;
    //    private readonly CompositionHost compositionContext;
    //    private readonly MefHostServices hostServices;

    //    private readonly ConcurrentDictionary<DocumentId, Action<DiagnosticsUpdatedArgs>> diagnosticsUpdatedNotifiers = new ConcurrentDictionary<DocumentId, Action<DiagnosticsUpdatedArgs>>();

    //    public RoslynHostOld()
    //    {
    //        compositionContext = CreateCompositionContext();

    //        // Create MEF host services
    //        hostServices = MefHostServices.Create(compositionContext);

    //        // Create default workspace
    //        workspace = new RoslynWorkspace(this);
    //        workspace.EnableDiagnostics();

    //        GetService<IDiagnosticService>().DiagnosticsUpdated += OnDiagnosticsUpdated;

    //        ParseOptions = CreateDefaultParseOptions();
    //    }

    //    private static CompositionHost CreateCompositionContext()
    //    {
    //        var assemblies = new[]
    //        {
    //            Assembly.Load("Microsoft.CodeAnalysis.Workspaces"),
    //            Assembly.Load("Microsoft.CodeAnalysis.CSharp.Workspaces"),
    //            Assembly.Load("Microsoft.CodeAnalysis.Features"),
    //            Assembly.Load("Microsoft.CodeAnalysis.CSharp.Features"),
    //            Assembly.Load("Microsoft.CodeAnalysis.Workspaces.MSBuild"),
    //            typeof(IRoslynHost).Assembly, // RoslynPad.Roslyn
    //            typeof(SymbolDisplayPartExtensions).Assembly, // RoslynPad.Roslyn.Windows
    //            typeof(AvalonEditTextContainer).Assembly, // RoslynPad.Editor.Windows
    //        };

    //        var partTypes = assemblies
    //            .SelectMany(x => x.DefinedTypes)
    //            .Select(x => x.AsType());

    //        return new ContainerConfiguration()
    //            .WithParts(partTypes)
    //            .CreateContainer();
    //    }

    //    internal static readonly ImmutableArray<string> PreprocessorSymbols =
    //        ImmutableArray.CreateRange(new[] { "TRACE", "DEBUG" });

    //    protected virtual ParseOptions CreateDefaultParseOptions()
    //    {
    //        return new CSharpParseOptions(kind: SourceCodeKind.Regular,
    //            preprocessorSymbols: PreprocessorSymbols, languageVersion: LanguageVersion.Latest);
    //    }

    //    /// <summary>
    //    /// The roslyn workspace.
    //    /// </summary>
    //    public RoslynWorkspace Workspace => workspace;

    //    /// <summary>
    //    /// The roslyn services.
    //    /// </summary>
    //    public MefHostServices HostServices => hostServices;

    //    public ParseOptions ParseOptions { get; }

    //    /// <summary>
    //    /// Gets a specific service.
    //    /// </summary>
    //    /// <typeparam name="TService">The type of service to get.</typeparam>
    //    /// <returns>The service if found, [null] otherwise.</returns>
    //    public TService GetService<TService>()
    //    {
    //        compositionContext.TryGetExport<TService>(out var service);
    //        return service;
    //    }

    //    private void OnDiagnosticsUpdated(object sender, DiagnosticsUpdatedArgs diagnosticsUpdatedArgs)
    //    {
    //        var documentId = diagnosticsUpdatedArgs?.DocumentId;
    //        if (documentId == null) return;

    //        Action<DiagnosticsUpdatedArgs> notifier;
    //        if (diagnosticsUpdatedNotifiers.TryGetValue(documentId, out notifier))
    //        {
    //            notifier(diagnosticsUpdatedArgs);
    //        }
    //    }

    //    public DocumentId AddDocument(DocumentCreationArgs args)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public Document GetDocument(DocumentId documentId)
    //    {
    //        return workspace.CurrentSolution.GetDocument(documentId);
    //    }

    //    public void CloseDocument(DocumentId documentId)
    //    {
    //        workspace.CloseDocument(documentId);
    //    }

    //    public MetadataReference CreateMetadataReference(string location)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}
}
