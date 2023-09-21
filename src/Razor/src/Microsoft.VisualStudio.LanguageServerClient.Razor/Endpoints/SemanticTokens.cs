// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using MVST = Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

internal partial class RazorCustomMessageTarget
{
    // Called by the Razor Language Server to provide ranged semantic tokens from the platform.
    [JsonRpcMethod(CustomMessageNames.RazorProvideSemanticTokensRangeEndpoint, UseSingleObjectParameterDeserialization = true)]
    public async Task<ProvideSemanticTokensResponse?> ProvideSemanticTokensAsync(
        ProvideSemanticTokensRangesParams semanticTokensParams,
        CancellationToken cancellationToken)
    {
        if (semanticTokensParams is null)
        {
            throw new ArgumentNullException(nameof(semanticTokensParams));
        }

        if (semanticTokensParams.Ranges is null)
        {
            throw new ArgumentNullException(nameof(semanticTokensParams.Ranges));
        }

        var (synchronized, csharpDoc) = await TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>((int)semanticTokensParams.RequiredHostDocumentVersion, semanticTokensParams.TextDocument, cancellationToken);

        if (csharpDoc is null)
        {
            return null;
        }

        if (!synchronized)
        {
            // If we're unable to synchronize we won't produce useful results, but we have to indicate
            // it's due to out of sync by providing the old version
            return new ProvideSemanticTokensResponse(tokens: null, hostDocumentSyncVersion: csharpDoc.HostDocumentSyncVersion ?? -1);
        }

        semanticTokensParams.TextDocument.Uri = csharpDoc.Uri;
        var textBuffer = csharpDoc.Snapshot.TextBuffer;
        var response = semanticTokensParams.Ranges.Length == 1 ?
            await ProvideMinimalRangeSemanticTokensAsync(semanticTokensParams, textBuffer, cancellationToken):
            await ProvidePreciseRangeSemanticTokensAsync(semanticTokensParams, textBuffer, cancellationToken);

        if (response is null && semanticTokensParams.Ranges.Length > 1)
        {
            // Likely the server doesn't support the new endpoint, fallback to the original one
            var minimalRange = new Range
            {
                Start = semanticTokensParams.Ranges.First().Start,
                End = semanticTokensParams.Ranges.Last().End
            };

            var newParams = new ProvideSemanticTokensRangesParams(
                semanticTokensParams.TextDocument,
                semanticTokensParams.RequiredHostDocumentVersion,
                new[] { minimalRange },
                semanticTokensParams.CorrelationId);

            return await ProvideSemanticTokensAsync(newParams, cancellationToken);
        }

        if (response?.Data is null)
        {
            _logger?.LogDebug("Made one semantic token request to Roslyn for {count} ranges but got null result back, due to sync issues", semanticTokensParams.Ranges.Length);
            // Weren't able to re-invoke C# semantic tokens but we have to indicate it's due to out of sync by providing the old version
            return new ProvideSemanticTokensResponse(tokens: null, hostDocumentSyncVersion: csharpDoc.HostDocumentSyncVersion ?? -1);
        }

        _logger?.LogDebug("Made one semantic token requests to Roslyn for {count} ranges", semanticTokensParams.Ranges.Length);
        return new ProvideSemanticTokensResponse(response.Data, semanticTokensParams.RequiredHostDocumentVersion);
    }

    private async Task<VSSemanticTokensResponse?> ProvideMinimalRangeSemanticTokensAsync(
        ProvideSemanticTokensRangesParams semanticTokensParams,
        MVST.ITextBuffer textBuffer,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
        var lspMethodName = Methods.TextDocumentSemanticTokensRangeName;
        Debug.Assert(semanticTokensParams.Ranges.Length == 1);
        var newParams = new SemanticTokensRangeParams
        {
            TextDocument = semanticTokensParams.TextDocument,
            Range = semanticTokensParams.Ranges[0],
        };

        using (var disposable = _telemetryReporter.TrackLspRequest(lspMethodName, languageServerName, semanticTokensParams.CorrelationId))
        {
            var result = await _requestInvoker.ReinvokeRequestOnServerAsync<SemanticTokensRangeParams, VSSemanticTokensResponse?>(
                textBuffer,
                lspMethodName,
                languageServerName,
                newParams,
                cancellationToken);

            return result?.Response;
        }
    }

    private async Task<VSSemanticTokensResponse?> ProvidePreciseRangeSemanticTokensAsync(
        ProvideSemanticTokensRangesParams semanticTokensParams,
        MVST.ITextBuffer textBuffer,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
        var lspMethodName = RazorLSPConstants.RoslynSemanticTokenRangesEndpointName;
        var newParams = new SemanticTokensRangesParams()
        {
            TextDocument = semanticTokensParams.TextDocument,
            Ranges = semanticTokensParams.Ranges
        };

        using (var disposable = _telemetryReporter.TrackLspRequest(lspMethodName, languageServerName, semanticTokensParams.CorrelationId))
        {
            var result = await _requestInvoker.ReinvokeRequestOnServerAsync<SemanticTokensRangesParams, VSSemanticTokensResponse?>(
                textBuffer,
                lspMethodName,
                languageServerName,
                SupportsPreciseRanges,
                newParams,
                cancellationToken).ConfigureAwait(false);

            return result?.Response;
        }
    }

    private static bool SupportsPreciseRanges(JToken token)
    {
        var serverCapabilities = token.ToObject<VSInternalServerCapabilities>();

        return serverCapabilities?.Experimental is JObject experimental
            && experimental.TryGetValue(RazorLSPConstants.RoslynSemanticTokenRangesEndpointName, out var supportsPreciseRanges)
            && supportsPreciseRanges.ToObject<bool>();
    }
}
