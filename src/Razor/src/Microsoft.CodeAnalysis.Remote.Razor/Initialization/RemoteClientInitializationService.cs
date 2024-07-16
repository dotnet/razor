// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor.SemanticTokens;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed class RemoteClientInitializationService(
    IRazorServiceBroker serviceBroker,
    RemoteLanguageServerFeatureOptions remoteLanguageServerFeatureOptions,
    RemoteSemanticTokensLegendService remoteSemanticTokensLegendService)
    : RazorServiceBase(serviceBroker), IRemoteClientInitializationService
{
    private readonly RemoteLanguageServerFeatureOptions _remoteLanguageServerFeatureOptions = remoteLanguageServerFeatureOptions;
    private readonly RemoteSemanticTokensLegendService _remoteSemanticTokensLegendService = remoteSemanticTokensLegendService;

    public ValueTask InitializeAsync(RemoteClientInitializationOptions options, CancellationToken cancellationToken)
        => RunServiceAsync(ct =>
            {
                _remoteLanguageServerFeatureOptions.SetOptions(options);
                return default;
            },
            cancellationToken);

    public ValueTask InitializeLSPAsync(RemoteClientLSPInitializationOptions options, CancellationToken cancellationToken)
        => RunServiceAsync(ct =>
            {
                _remoteSemanticTokensLegendService.SetLegend(options.TokenTypes, options.TokenModifiers);
                return default;
            },
            cancellationToken);
}
