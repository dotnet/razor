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
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
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
        if (semanticTokensParams is null)
        {
            throw new ArgumentNullException(nameof(semanticTokensParams));
        }

        if (semanticTokensParams.Range is null)
        {
            throw new ArgumentNullException(nameof(semanticTokensParams.Range));
        }

        var (synchronized, csharpDoc) = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
            _documentManager, (int)semanticTokensParams.RequiredHostDocumentVersion, semanticTokensParams.TextDocument, cancellationToken);

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

        var newParams = new SemanticTokensRangeParams
        {
            TextDocument = semanticTokensParams.TextDocument,
            PartialResultToken = semanticTokensParams.PartialResultToken,
            Range = semanticTokensParams.Range,
        };

        var textBuffer = csharpDoc.Snapshot.TextBuffer;
        var languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
        var lspMethodName = Methods.TextDocumentSemanticTokensRangeName;
        using var _ = _telemetryReporter.TrackLspRequest(lspMethodName, languageServerName, semanticTokensParams.CorrelationId);
        var csharpResults = await _requestInvoker.ReinvokeRequestOnServerAsync<SemanticTokensRangeParams, VSSemanticTokensResponse>(
            textBuffer,
            lspMethodName,
            languageServerName,
            newParams,
            cancellationToken).ConfigureAwait(false);

        var result = csharpResults?.Response;
        if (result is null)
        {
            // Weren't able to re-invoke C# semantic tokens but we have to indicate it's due to out of sync by providing the old version
            return new ProvideSemanticTokensResponse(tokens: null, hostDocumentSyncVersion: csharpDoc.HostDocumentSyncVersion);
        }

        var response = new ProvideSemanticTokensResponse(result.Data, semanticTokensParams.RequiredHostDocumentVersion);

        return response;
    }

    // Called by the Razor Language Server to provide ranged semantic tokens from the platform.
    [JsonRpcMethod(CustomMessageNames.RazorProvideSemanticTokensRangesEndpoint, UseSingleObjectParameterDeserialization = true)]
    public async Task<ProvideSemanticTokensResponse?> ProvideSemanticTokensRangesAsync(
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

        var (synchronized, csharpDoc) = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
            _documentManager, (int)semanticTokensParams.RequiredHostDocumentVersion, semanticTokensParams.TextDocument, cancellationToken);

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

        // Ensure the C# ranges are sorted
        Array.Sort(semanticTokensParams.Ranges, static (r1, r2) => r1.CompareTo(r2));

        semanticTokensParams.TextDocument.Uri = csharpDoc.Uri;
        var requestTasks = new List<Task<ReinvocationResponse<VSSemanticTokensResponse>?>>(semanticTokensParams.Ranges.Length);
        var textBuffer = csharpDoc.Snapshot.TextBuffer;
        var languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
        var lspMethodName = Methods.TextDocumentSemanticTokensRangeName;

        foreach (var range in semanticTokensParams.Ranges)
        {
            var newParams = new SemanticTokensRangeParams
            {
                TextDocument = semanticTokensParams.TextDocument,
                PartialResultToken = semanticTokensParams.PartialResultToken,
                Range = range,
            };

            using var _ = _telemetryReporter.TrackLspRequest(lspMethodName, languageServerName, semanticTokensParams.CorrelationId);
            var task = _requestInvoker.ReinvokeRequestOnServerAsync<SemanticTokensRangeParams, VSSemanticTokensResponse>(
                textBuffer,
                lspMethodName,
                languageServerName,
                newParams,
                cancellationToken);
            requestTasks.Add(task);
        }

        var results = await Task.WhenAll(requestTasks).ConfigureAwait(false);
        var nonEmptyResults = results.Select(r => r?.Response?.Data).WithoutNull().ToArray();

        if (nonEmptyResults.Length != semanticTokensParams.Ranges.Length)
        {
            // Weren't able to re-invoke C# semantic tokens but we have to indicate it's due to out of sync by providing the old version
            return new ProvideSemanticTokensResponse(tokens: null, hostDocumentSyncVersion: csharpDoc.HostDocumentSyncVersion);
        }

        var data = StitchSemanticTokenResponsesTogether(nonEmptyResults);

        var response = new ProvideSemanticTokensResponse(data, semanticTokensParams.RequiredHostDocumentVersion);

        return response;
    }

    private int[] StitchSemanticTokenResponsesTogether(int[][] responseData)
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
