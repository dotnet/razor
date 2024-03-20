// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.SemanticTokens;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Razor.Workspaces.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Cohost;

[Shared]
[Export(typeof(IRazorCohostDynamicRegistrationService))]
[Export(typeof(IClientCapabilitiesService))]
[method: ImportingConstructor]
internal class RazorCohostDynamicRegistrationService(LanguageServerFeatureOptions languageServerFeatureOptions, Lazy<ISemanticTokensLegendService> semanticTokensLegendService, ILoggerFactory loggerFactory) : IRazorCohostDynamicRegistrationService, IClientCapabilitiesService
{
    private readonly string _id = Guid.NewGuid().ToString();
    private readonly DocumentFilter[] _filter = [new DocumentFilter()
    {
        Language = Constants.RazorLanguageName,
        Pattern = "**/*.{razor,cshtml}"
    }];

    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions = languageServerFeatureOptions;
    private readonly Lazy<ISemanticTokensLegendService> _semanticTokensLegendService = semanticTokensLegendService;
    private readonly ILogger _logger = loggerFactory.CreateLogger<RazorCohostDynamicRegistrationService>();

    private VSInternalClientCapabilities? _clientCapabilities;

    public bool CanGetClientCapabilities => _clientCapabilities is not null;

    public VSInternalClientCapabilities ClientCapabilities => _clientCapabilities.AssumeNotNull();

    public async Task RegisterAsync(string clientCapabilities, IRazorCohostClientLanguageServerManager razorCohostClientLanguageServerManager, CancellationToken cancellationToken)
    {
        if (!_languageServerFeatureOptions.UseRazorCohostServer)
        {
            return;
        }

        _clientCapabilities = JsonConvert.DeserializeObject<VSInternalClientCapabilities>(clientCapabilities) ?? new();

        // TODO: Get the options from the from the endpoints somehow
        if (_clientCapabilities.TextDocument?.SemanticTokens?.DynamicRegistration == true)
        {
            await razorCohostClientLanguageServerManager.SendRequestAsync(
                Methods.ClientRegisterCapabilityName,
                new RegistrationParams()
                {
                    Registrations = [
                        new Registration()
                        {
                            Id = _id,
                            Method = Methods.TextDocumentSemanticTokensRangeName,
                            RegisterOptions = new SemanticTokensRegistrationOptions()
                            {
                                DocumentSelector = _filter,
                            }.EnableSemanticTokens(_semanticTokensLegendService.Value)
                        }
                    ]
                },
                cancellationToken).ConfigureAwait(false);
        }
    }
}
