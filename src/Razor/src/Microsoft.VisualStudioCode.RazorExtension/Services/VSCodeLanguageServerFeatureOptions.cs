﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.NET.Sdk.Razor.SourceGenerators;
using Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

namespace Microsoft.VisualStudioCode.RazorExtension.Services;

[Shared]
[Export(typeof(LanguageServerFeatureOptions))]
internal class VSCodeLanguageServerFeatureOptions : LanguageServerFeatureOptions, IRazorCohostStartupService
{
    private bool _useRazorCohostServer = false;
    private bool _useNewFormattingEngine = true;
    private bool _forceRuntimeCodeGeneration = false;

    // Options that are set to their defaults
    public override bool SupportsFileManipulation => true;
    public override bool SingleServerSupport => false;
    public override bool UsePreciseSemanticTokenRanges => false;
    public override bool ShowAllCSharpCodeActions => false;
    public override bool ReturnCodeActionAndRenamePathsWithPrefixedSlash => PlatformInformation.IsWindows;
    public override bool IncludeProjectKeyInGeneratedFilePath => false;
    public override bool DoNotInitializeMiscFilesProjectFromWorkspace => false;

    // Options that differ from the default
    public override string CSharpVirtualDocumentSuffix => "__virtual.cs";
    public override string HtmlVirtualDocumentSuffix => "__virtual.html";
    public override bool UpdateBuffersForClosedDocuments => true;
    public override bool DelegateToCSharpOnDiagnosticPublish => true;
    public override bool SupportsSoftSelectionInCompletion => false;
    public override bool UseVsCodeCompletionTriggerCharacters => true;

    // User configurable options
    public override bool UseRazorCohostServer => _useRazorCohostServer;
    public override bool ForceRuntimeCodeGeneration => _forceRuntimeCodeGeneration;
    public override bool UseNewFormattingEngine => _useNewFormattingEngine;

    public async Task StartupAsync(VSInternalClientCapabilities clientCapabilities, RazorCohostRequestContext requestContext, CancellationToken cancellationToken)
    {
        var razorClientLanguageServerManager = requestContext.GetRequiredService<IRazorClientLanguageServerManager>();

        // Attempt to get configurations from the client.  If this throws we'll get NFW reports.
        var configurationParams = new ConfigurationParams()
        {
            Items = [
                // Roslyn's typescript config handler will convert underscores to camelcase, ie 'razor.languageServer.cohostingEnabled'
                new ConfigurationItem { Section = "razor.language_server.cohosting_enabled" },
                new ConfigurationItem { Section = "razor.language_server.use_new_formatting_engine" },
                new ConfigurationItem { Section = "razor.language_server.force_runtime_code_generation" },

            ]
        };
        var options = await razorClientLanguageServerManager.SendRequestAsync<ConfigurationParams, JsonArray>(
            Methods.WorkspaceConfigurationName,
            configurationParams,
            cancellationToken).ConfigureAwait(false);

        _useRazorCohostServer = GetBooleanOptionValue(options[0], _useRazorCohostServer);
        _useNewFormattingEngine = GetBooleanOptionValue(options[1], _useNewFormattingEngine);
        _forceRuntimeCodeGeneration = GetBooleanOptionValue(options[2], _forceRuntimeCodeGeneration);

        RazorCohostingOptions.UseRazorCohostServer = _useRazorCohostServer;
    }

    private static bool GetBooleanOptionValue(JsonNode? jsonNode, bool defaultValue)
    {
        if (jsonNode is null)
        {
            return defaultValue;
        }

        return jsonNode.ToString() == "true";
    }
}
