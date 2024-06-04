// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Razor;

[Export(typeof(LanguageServerFeatureOptions))]
internal class VisualStudioLanguageServerFeatureOptions : LanguageServerFeatureOptions
{
    private readonly IFeatureFlagService _featureFlagService;
    private readonly ILspEditorFeatureDetector _lspEditorFeatureDetector;
    private readonly JoinableTaskContext _joinableTaskContext;

    private readonly AsyncLazy<bool> _includeProjectKeyInGeneratedFilePath;
    private readonly AsyncLazy<bool> _showAllCSharpCodeActions;
    private readonly AsyncLazy<bool> _usePreciseSemanticTokenRanges;
    private readonly AsyncLazy<bool> _useRazorCohostServer;
    private readonly AsyncLazy<bool> _disableRazorLanguageServer;
    private readonly AsyncLazy<bool> _forceRuntimeCodeGeneration;
    private readonly AsyncLazy<bool> _useProjectConfigurationEndpoint;

    [ImportingConstructor]
    public VisualStudioLanguageServerFeatureOptions(
        IFeatureFlagService featureFlagService,
        ILspEditorFeatureDetector lspEditorFeatureDetector,
        JoinableTaskContext joinableTaskContext)
    {
        _featureFlagService = featureFlagService;
        _lspEditorFeatureDetector = lspEditorFeatureDetector;
        _joinableTaskContext = joinableTaskContext;

        // On by default
        _includeProjectKeyInGeneratedFilePath = IsFeatureEnabledOrTrue(WellKnownFeatureFlagNames.IncludeProjectKeyInGeneratedFilePath);

        // Off by default
        _showAllCSharpCodeActions = IsFeatureEnabledOrFalse(WellKnownFeatureFlagNames.ShowAllCSharpCodeActions);
        _usePreciseSemanticTokenRanges = IsFeatureEnabledOrFalse(WellKnownFeatureFlagNames.UsePreciseSemanticTokenRanges);
        _useRazorCohostServer = IsFeatureEnabledOrFalse(WellKnownFeatureFlagNames.UseRazorCohostServer);
        _disableRazorLanguageServer = IsFeatureEnabledOrFalse(WellKnownFeatureFlagNames.DisableRazorLanguageServer);
        _forceRuntimeCodeGeneration = IsFeatureEnabledOrFalse(WellKnownFeatureFlagNames.ForceRuntimeCodeGeneration);
        _useProjectConfigurationEndpoint = IsFeatureEnabledOrFalse(WellKnownFeatureFlagNames.UseProjectConfigurationEndpoint);
    }

    private AsyncLazy<bool> IsFeatureEnabledOrFalse(string featureName)
        => new(async () => await _featureFlagService.IsFeatureEnabledAsync(featureName, defaultValue: false), _joinableTaskContext.Factory);

    private AsyncLazy<bool> IsFeatureEnabledOrTrue(string featureName)
        => new(async () => await _featureFlagService.IsFeatureEnabledAsync(featureName, defaultValue: true), _joinableTaskContext.Factory);

    // We don't currently support file creation operations on VS CodeSpaces or VS Live Share
    public override bool SupportsFileManipulation => !IsCodeSpacesOrLiveShare;

    // In VS we override the project configuration file name because we don't want our serialized state to clash with other platforms (VSCode)
    public override string ProjectConfigurationFileName => "project.razor.vs.bin";

    public override string CSharpVirtualDocumentSuffix => ".ide.g.cs";

    public override string HtmlVirtualDocumentSuffix => "__virtual.html";

    public override bool SingleServerCompletionSupport => true;

    public override bool SingleServerSupport => true;

    public override bool DelegateToCSharpOnDiagnosticPublish => false;

    public override bool ReturnCodeActionAndRenamePathsWithPrefixedSlash => false;

    public override bool UpdateBuffersForClosedDocuments => false;

    private bool IsCodeSpacesOrLiveShare => _lspEditorFeatureDetector.IsRemoteClient() || _lspEditorFeatureDetector.IsLiveShareHost();

    public override bool ShowAllCSharpCodeActions => _showAllCSharpCodeActions.GetValue();

    public override bool IncludeProjectKeyInGeneratedFilePath => _includeProjectKeyInGeneratedFilePath.GetValue();

    public override bool UsePreciseSemanticTokenRanges => _usePreciseSemanticTokenRanges.GetValue();

    public override bool MonitorWorkspaceFolderForConfigurationFiles => false;

    public override bool UseRazorCohostServer => _useRazorCohostServer.GetValue();

    public override bool DisableRazorLanguageServer => _disableRazorLanguageServer.GetValue();

    /// <inheritdoc />
    public override bool ForceRuntimeCodeGeneration => _forceRuntimeCodeGeneration.GetValue();

    /// <inheritdoc />
    public override bool UseProjectConfigurationEndpoint => _useProjectConfigurationEndpoint.GetValue();
}
