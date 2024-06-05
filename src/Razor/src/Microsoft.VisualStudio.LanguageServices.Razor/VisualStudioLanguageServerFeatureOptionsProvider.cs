// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualStudio.Razor;

[Export(typeof(ILanguageServerFeatureOptionsProvider))]
[method: ImportingConstructor]
internal sealed class VisualStudioLanguageServerFeatureOptionsProvider(
    [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider,
    ILspEditorFeatureDetector lspEditorFeatureDetector)
    : ILanguageServerFeatureOptionsProvider
{
    private readonly Lazy<Options> _lazyOptions = new(() => CreateOptions(serviceProvider, lspEditorFeatureDetector));

    public LanguageServerFeatureOptions GetOptions()
        => _lazyOptions.Value;

    private static Options CreateOptions(IServiceProvider serviceProvider, ILspEditorFeatureDetector lspEditorFeatureDetector)
    {
        var optionsProvider = (IVisualStudioOptionsProvider)serviceProvider.GetService(typeof(IVisualStudioOptionsProvider));

        // On by default
        var includeProjectKeyInGeneratedFilePath = optionsProvider.IsFeatureEnabled(WellKnownFeatureFlagNames.IncludeProjectKeyInGeneratedFilePath, defaultValue: true);

        // Off by default
        var disableRazorLanguageServer = optionsProvider.IsFeatureEnabled(WellKnownFeatureFlagNames.DisableRazorLanguageServer);
        var forceRuntimeCodeGeneration = optionsProvider.IsFeatureEnabled(WellKnownFeatureFlagNames.ForceRuntimeCodeGeneration);
        var showAllCSharpCodeActions = optionsProvider.IsFeatureEnabled(WellKnownFeatureFlagNames.ShowAllCSharpCodeActions);
        var usePreciseSemanticTokenRanges = optionsProvider.IsFeatureEnabled(WellKnownFeatureFlagNames.UsePreciseSemanticTokenRanges);
        var useProjectConfigurationEndpoint = optionsProvider.IsFeatureEnabled(WellKnownFeatureFlagNames.UseProjectConfigurationEndpoint);
        var useRazorCohostServer = optionsProvider.IsFeatureEnabled(WellKnownFeatureFlagNames.UseRazorCohostServer);

        return new Options(
            lspEditorFeatureDetector,
            disableRazorLanguageServer,
            includeProjectKeyInGeneratedFilePath,
            forceRuntimeCodeGeneration,
            showAllCSharpCodeActions,
            usePreciseSemanticTokenRanges,
            useProjectConfigurationEndpoint,
            useRazorCohostServer);
    }

    private sealed class Options(
        ILspEditorFeatureDetector lspEditorFeatureDetector,
        bool disableRazorLanguageServer,
        bool includeProjectKeyInGeneratedFilePath,
        bool forceRuntimeCodeGeneration,
        bool showAllCSharpCodeActions,
        bool usePreciseSemanticTokenRanges,
        bool useProjectConfigurationEndpoint,
        bool useRazorCohostServer)
        : LanguageServerFeatureOptions
    {
        // In VS we override the project configuration file name because we don't
        // want our serialized state to clash with other platforms (VSCode)
        public override string ProjectConfigurationFileName => "project.razor.vs.bin";
        public override string CSharpVirtualDocumentSuffix => ".ide.g.cs";
        public override string HtmlVirtualDocumentSuffix => "__virtual.html";

        public override bool DelegateToCSharpOnDiagnosticPublish => false;
        public override bool MonitorWorkspaceFolderForConfigurationFiles => false;
        public override bool ReturnCodeActionAndRenamePathsWithPrefixedSlash => false;
        public override bool SingleServerCompletionSupport => true;
        public override bool SingleServerSupport => true;
        public override bool UpdateBuffersForClosedDocuments => false;

        // We don't currently support file creation operations on VS CodeSpaces or VS Live Share
        public override bool SupportsFileManipulation
            => !lspEditorFeatureDetector.IsRemoteClient() &&
               !lspEditorFeatureDetector.IsLiveShareHost();

        public override bool DisableRazorLanguageServer { get; } = disableRazorLanguageServer;
        public override bool ForceRuntimeCodeGeneration { get; } = forceRuntimeCodeGeneration;
        public override bool IncludeProjectKeyInGeneratedFilePath { get; } = includeProjectKeyInGeneratedFilePath;
        public override bool ShowAllCSharpCodeActions { get; } = showAllCSharpCodeActions;
        public override bool UsePreciseSemanticTokenRanges { get; } = usePreciseSemanticTokenRanges;
        public override bool UseProjectConfigurationEndpoint { get; } = useProjectConfigurationEndpoint;
        public override bool UseRazorCohostServer { get; } = useRazorCohostServer;
    }
}
