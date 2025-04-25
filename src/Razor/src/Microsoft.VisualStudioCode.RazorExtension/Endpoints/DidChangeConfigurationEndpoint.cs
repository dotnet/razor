// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Features;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.VisualStudio.Razor.LanguageClient.Cohost;
using Microsoft.VisualStudioCode.RazorExtension.Configuration;

namespace Microsoft.VisualStudioCode.RazorExtension.Endpoints;

[Shared]
[CohostEndpoint(Methods.WorkspaceDidChangeConfigurationName)]
[ExportRazorStatelessLspService(typeof(DidChangeConfigurationEndpoint))]
[method: ImportingConstructor]
internal class DidChangeConfigurationEndpoint(
    ClientSettingsReader clientSettingsReader,
    ILoggerFactory loggerFactory) : AbstractRazorCohostRequestHandler<DidChangeConfigurationParams, object?>, IDynamicRegistrationProvider, ICohostStartupService
{
    private readonly ClientSettingsReader _clientSettingsReader = clientSettingsReader;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<DidChangeConfigurationEndpoint>();

    protected override bool MutatesSolutionState => false;
    protected override bool RequiresLSPSolution => false;

    public ImmutableArray<Registration> GetRegistrations(VSInternalClientCapabilities clientCapabilities, RazorCohostRequestContext requestContext)
    {
        if (clientCapabilities.Workspace?.DidChangeConfiguration?.DynamicRegistration is true)
        {
            return [new Registration
                {
                    Method = Methods.WorkspaceDidChangeConfigurationName
                }];
        }

        return [];
    }

    public Task StartupAsync(string serializedClientCapabilities, RazorCohostRequestContext requestContext, CancellationToken cancellationToken)
    {
        return RefreshOptionsAsync(requestContext, cancellationToken);
    }

    protected override Task<object?> HandleRequestAsync(DidChangeConfigurationParams request, RazorCohostRequestContext context, CancellationToken cancellationToken)
    {
        _ = RefreshOptionsAsync(context, cancellationToken);
        return Task.FromResult<object?>(null);
    }

    private async Task RefreshOptionsAsync(RazorCohostRequestContext requestContext, CancellationToken cancellationToken)
    {
        _logger.LogDebug($"Refreshing options from client.");

        var razorClientLanguageServerManager = requestContext.GetRequiredService<IRazorClientLanguageServerManager>();

        var configurationParams = new ConfigurationParams()
        {
            Items = [
                //TODO: new ConfigurationItem { Section = "razor.format.enable" },
                new ConfigurationItem { Section = "razor.format.codeBlockBraceOnNextLine" },
                new ConfigurationItem { Section = "razor.completion.commitElementsWithSpace" },
            ]
        };

        var options = await razorClientLanguageServerManager.SendRequestAsync<ConfigurationParams, JsonArray>(
            Methods.WorkspaceConfigurationName,
            configurationParams,
            cancellationToken).ConfigureAwait(false);

        var current = _clientSettingsReader.GetClientSettings().AdvancedSettings;
        var settings = current with
        {
            CodeBlockBraceOnNextLine = GetBooleanOptionValue(options[0], current.CodeBlockBraceOnNextLine),
            CommitElementsWithSpace = GetBooleanOptionValue(options[1], current.CommitElementsWithSpace),
        };

        _clientSettingsReader.Update(settings);
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
