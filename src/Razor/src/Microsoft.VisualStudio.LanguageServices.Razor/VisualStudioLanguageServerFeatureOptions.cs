// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudio.Razor;

[Export(typeof(LanguageServerFeatureOptions))]
internal class VisualStudioLanguageServerFeatureOptions : LanguageServerFeatureOptions
{
    private readonly IFeatureFlagService _featureFlagService;
    private readonly ILspEditorFeatureDetector _lspEditorFeatureDetector;

    private readonly Lazy<bool> _showAllCSharpCodeActions;
    private readonly Lazy<bool> _includeProjectKeyInGeneratedFilePath;
    private readonly Lazy<bool> _usePreciseSemanticTokenRanges;
    private readonly Lazy<bool> _useRazorCohostServer;
    private readonly Lazy<bool> _disableRazorLanguageServer;
    private readonly Lazy<bool> _forceRuntimeCodeGeneration;
    private readonly Lazy<bool> _useProjectConfigurationEndpoint;

    [ImportingConstructor]
    public VisualStudioLanguageServerFeatureOptions(
        IFeatureFlagService featureFlagService,
        ILspEditorFeatureDetector lspEditorFeatureDetector)
    {
        _featureFlagService = featureFlagService;
        _lspEditorFeatureDetector = lspEditorFeatureDetector;

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

    private Lazy<bool> IsFeatureEnabledOrFalse(string featureName)
        => new(() => _featureFlagService.IsFeatureEnabled(featureName, defaultValue: false));

    private Lazy<bool> IsFeatureEnabledOrTrue(string featureName)
        => new(() => _featureFlagService.IsFeatureEnabled(featureName, defaultValue: true));

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

    public override bool ShowAllCSharpCodeActions => _showAllCSharpCodeActions.Value;

    public override bool IncludeProjectKeyInGeneratedFilePath => _includeProjectKeyInGeneratedFilePath.Value;

    public override bool UsePreciseSemanticTokenRanges => _usePreciseSemanticTokenRanges.Value;

    public override bool MonitorWorkspaceFolderForConfigurationFiles => false;

    public override bool UseRazorCohostServer => _useRazorCohostServer.Value;

    public override bool DisableRazorLanguageServer => _disableRazorLanguageServer.Value;

    /// <inheritdoc />
    public override bool ForceRuntimeCodeGeneration => _forceRuntimeCodeGeneration.Value;

    /// <inheritdoc />
    public override bool UseProjectConfigurationEndpoint => _useProjectConfigurationEndpoint.Value;
}
