// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text;
using StreamJsonRpc;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

internal partial class RazorCustomMessageTarget
{
    // Called by the Razor Language Server to provide ranged semantic tokens from the platform.
    [JsonRpcMethod(CustomMessageNames.RazorProvideSemanticTokensRangeEndpoint, UseSingleObjectParameterDeserialization = true)]
    public async Task<ProvideSemanticTokensResponse?> ProvideSemanticTokensRangeAsync(
        ProvideSemanticTokensRangeParams semanticTokensParams,
        CancellationToken cancellationToken)
    {
        return await ProvideSemanticTokensHelperAsync(semanticTokensParams, cancellationToken);
    }

    [JsonRpcMethod(CustomMessageNames.RazorProvideSemanticTokensRangesEndpoint, UseSingleObjectParameterDeserialization = true)]
    public async Task<ProvideSemanticTokensResponse?> ProvideSemanticTokensRangesAsync(
        ProvideSemanticTokensRangesParams semanticTokensParams,
        CancellationToken cancellationToken)
    {
        return await ProvideSemanticTokensHelperAsync(semanticTokensParams, cancellationToken);
    }

    private async Task<ProvideSemanticTokensResponse?> ProvideSemanticTokensHelperAsync(
        ProvideSemanticTokensParams? semanticTokensParams,
        CancellationToken cancellationToken)
    {
        if (semanticTokensParams is null)
        {
            throw new ArgumentNullException(nameof(semanticTokensParams));
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
            return new ProvideSemanticTokensResponse(tokens: null, hostDocumentSyncVersion: -1);
        }

        semanticTokensParams.TextDocument.Uri = csharpDoc.Uri;
        var languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
        var lspMethodName = Methods.TextDocumentSemanticTokensRangeName;
        var (response, failed) = await ReinvokeSemanticTokensRequestOnServerAsync(
            csharpDoc.Snapshot,
            lspMethodName,
            languageServerName,
            semanticTokensParams,
            cancellationToken);

        if (failed)
        {
            // Weren't able to re-invoke C# semantic tokens but we have to indicate it's due to out of sync by providing the old version
            return new ProvideSemanticTokensResponse(tokens: null, hostDocumentSyncVersion: csharpDoc.HostDocumentSyncVersion);
        }

        return response;
    }

    private async Task<(ProvideSemanticTokensResponse?, bool)> ReinvokeSemanticTokensRequestOnServerAsync(
        ITextSnapshot snapshot,
        string lspMethodName,
        string languageServerName,
        ProvideSemanticTokensParams semanticTokensParams,
        CancellationToken cancellationToken)
    {
        var failed = false;
        ProvideSemanticTokensResponse? response = null;
        var trimRanges = semanticTokensParams is ProvideSemanticTokensRangesParams;

        if (!trimRanges)
        {
            var rangeParams = semanticTokensParams as ProvideSemanticTokensRangeParams ?? throw new ArgumentNullException(nameof(ProvideSemanticTokensRangeParams));
            if (rangeParams.Range is null)
            {
                throw new ArgumentNullException(nameof(rangeParams.Range));
            }

            var newParams = new SemanticTokensRangeParams
            {
                TextDocument = semanticTokensParams.TextDocument,
                PartialResultToken = semanticTokensParams.PartialResultToken,
                Range = rangeParams!.Range,
            };

            using var _ = _telemetryReporter.TrackLspRequest(lspMethodName, languageServerName, semanticTokensParams.CorrelationId);
            var csharpResults = await _requestInvoker.ReinvokeRequestOnServerAsync<SemanticTokensRangeParams, VSSemanticTokensResponse>(
                snapshot.TextBuffer,
                lspMethodName,
                languageServerName,
                newParams,
                cancellationToken).ConfigureAwait(false);

            var result = csharpResults?.Response;
            failed = result is null;
            if (!failed)
            {
                response = new ProvideSemanticTokensResponse(result!.Data, semanticTokensParams.RequiredHostDocumentVersion);
            }
        }
        else
        {
            var rangesParams = semanticTokensParams as ProvideSemanticTokensRangesParams ?? throw new ArgumentNullException(nameof(ProvideSemanticTokensRangesParams));
            if (rangesParams.Ranges is null)
            {
                throw new ArgumentNullException(nameof(rangesParams.Ranges));
            }

            // Ensure the C# ranges are sorted
            Array.Sort(rangesParams!.Ranges, static (r1, r2) => r1.CompareTo(r2));
            var requestTasks = new List<Task<ReinvocationResponse<VSSemanticTokensResponse>?>>(rangesParams!.Ranges.Length);

            foreach (var range in rangesParams!.Ranges)
            {
                var newParams = new SemanticTokensRangeParams
                {
                    TextDocument = semanticTokensParams.TextDocument,
                    PartialResultToken = semanticTokensParams.PartialResultToken,
                    Range = range,
                };

                var task = Task.Run(async () =>
                {
                    using (var disposable = _telemetryReporter.TrackLspRequest(lspMethodName, languageServerName, semanticTokensParams.CorrelationId))
                    {
                        return await _requestInvoker.ReinvokeRequestOnServerAsync<SemanticTokensRangeParams, VSSemanticTokensResponse>(
                            snapshot.TextBuffer,
                            lspMethodName,
                            languageServerName,
                            newParams,
                            cancellationToken);
                    }
                });
                requestTasks!.Add(task);
            }

            var results = await Task.WhenAll(requestTasks!).ConfigureAwait(false);
            var nonEmptyResults = results.Select(r => r?.Response?.Data).WithoutNull().ToArray();
            if (nonEmptyResults.Length != rangesParams!.Ranges.Length)
            {
                failed = true;
            }
            else
            {
                var data = StitchSemanticTokenResponsesTogether(nonEmptyResults);
                response = new ProvideSemanticTokensResponse(data, semanticTokensParams.RequiredHostDocumentVersion);
            }
        }

        return (response, failed);
    }

    // Internal for testing
    internal static int[] StitchSemanticTokenResponsesTogether(int[][] responseData)
    {
        var count = responseData.Sum(r => r.Length);
        var data = new int[count];
        var dataIndex = 0;
        var lastTokenLine = 0;

        for (var i = 0; i < responseData.Length; i++)
        {
            var curData = responseData[i];

            if (curData.Length == 0)
            {
                continue;
            }

            Array.Copy(curData, 0, data, dataIndex, curData.Length);
            if (i != 0)
            {
                // The first two items in result.Data will potentially need it's line/col offset modified
                var lineDelta = data[dataIndex] - lastTokenLine;

                // Update the first line copied over from curData
                data[dataIndex] = lineDelta;

                // Update the first column copied over from curData if on the same line as the previous token
                if (lineDelta == 0)
                {
                    var lastTokenCol = 0;

                    // Walk back accumulating column deltas until we find a start column (indicated by it's line offset being non-zero)
                    for (var j = dataIndex - RazorSemanticTokensInfoService.TokenSize; j >= 0; j -= RazorSemanticTokensInfoService.TokenSize)
                    {
                        lastTokenCol += data[dataIndex + 1];
                        if (data[dataIndex] != 0)
                        {
                            break;
                        }
                    }

                    data[dataIndex + 1] -= lastTokenCol;
                }
            }

            lastTokenLine = 0;
            for (var j = 0; j < curData.Length; j += RazorSemanticTokensInfoService.TokenSize)
            {
                lastTokenLine += curData[j];
            }

            dataIndex += curData.Length;
        }

        return data;
    }
}
