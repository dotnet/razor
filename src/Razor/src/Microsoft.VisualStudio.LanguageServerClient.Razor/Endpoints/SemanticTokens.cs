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
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using StreamJsonRpc;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

internal partial class RazorCustomMessageTarget
{
    // Called by the Razor Language Server to provide ranged semantic tokens from the platform.
    [JsonRpcMethod(CustomMessageNames.RazorProvideSemanticTokensRangeEndpoint, UseSingleObjectParameterDeserialization = true)]
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

        // Ensure the C# ranges are sorted
        Array.Sort(semanticTokensParams.Ranges, static (r1, r2) => r1.CompareTo(r2));
        using var _ = ListPool<Task<ReinvocationResponse<VSSemanticTokensResponse>?>>.GetPooledObject(out var requestTasks);

        semanticTokensParams.TextDocument.Uri = csharpDoc.Uri;
        var textBuffer = csharpDoc.Snapshot.TextBuffer;
        var languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
        var lspMethodName = Methods.TextDocumentSemanticTokensRangeName;

        foreach (var range in semanticTokensParams!.Ranges)
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
                        textBuffer,
                        lspMethodName,
                        languageServerName,
                        newParams,
                        cancellationToken);
                }
            });

            requestTasks.Add(task);
        }

        var results = await Task.WhenAll(requestTasks).ConfigureAwait(false);
        var nonEmptyResults = results.Select(r => r?.Response?.Data).WithoutNull().ToArray();
        if (nonEmptyResults.Length != semanticTokensParams!.Ranges.Length)
        {
            // Weren't able to re-invoke C# semantic tokens but we have to indicate it's due to out of sync by providing the old version
            return new ProvideSemanticTokensResponse(tokens: null, hostDocumentSyncVersion: csharpDoc.HostDocumentSyncVersion);
        }

        return new ProvideSemanticTokensResponse(nonEmptyResults, semanticTokensParams.RequiredHostDocumentVersion);
    }
}
