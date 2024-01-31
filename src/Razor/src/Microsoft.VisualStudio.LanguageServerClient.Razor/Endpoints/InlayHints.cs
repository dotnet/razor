// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using StreamJsonRpc;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

internal partial class RazorCustomMessageTarget
{
    [JsonRpcMethod(CustomMessageNames.RazorInlayHintEndpoint, UseSingleObjectParameterDeserialization = true)]
    public async Task<InlayHint[]?> ProvideInlayHintsAsync(DelegatedInlayHintParams request, CancellationToken cancellationToken)
    {
        var delegationDetails = await GetProjectedRequestDetailsAsync(request, cancellationToken).ConfigureAwait(false);
        if (delegationDetails is null)
        {
            return default;
        }

        var inlayHintParams = new InlayHintParams
        {
            TextDocument = request.Identifier.TextDocumentIdentifier.WithUri(delegationDetails.Value.ProjectedUri),
            Range = request.ProjectedRange
        };

        var response = await _requestInvoker.ReinvokeRequestOnServerAsync<InlayHintParams, InlayHint[]?>(
            delegationDetails.Value.TextBuffer,
            Methods.TextDocumentInlayHintName,
            delegationDetails.Value.LanguageServerName,
            inlayHintParams,
            cancellationToken).ConfigureAwait(false);
        return response?.Response;
    }

    [JsonRpcMethod(CustomMessageNames.RazorInlayHintResolveEndpoint, UseSingleObjectParameterDeserialization = true)]
    public async Task<InlayHint?> ProvideInlayHintsResolveAsync(DelegatedInlayHintResolveParams request, CancellationToken cancellationToken)
    {
        // We don't really need the text document for inlay hint resolve, but we need to at least know which text
        // buffer should get the request, so we just ask for any version above version 0, to get the right buffer.
        string languageServerName;
        var synchronized = false;
        VirtualDocumentSnapshot? virtualDocumentSnapshot = null;
        if (request.ProjectedKind == RazorLanguageKind.Html)
        {
            var syncResult = TryReturnPossiblyFutureSnapshot<HtmlVirtualDocumentSnapshot>(0, request.Identifier);
            if (syncResult?.Synchronized == true)
            {
                virtualDocumentSnapshot = syncResult.VirtualSnapshot;
            }

            languageServerName = RazorLSPConstants.HtmlLanguageServerName;
        }
        else if (request.ProjectedKind == RazorLanguageKind.CSharp)
        {
            var syncResult = TryReturnPossiblyFutureSnapshot<CSharpVirtualDocumentSnapshot>(0, request.Identifier);
            if (syncResult?.Synchronized == true)
            {
                virtualDocumentSnapshot = syncResult.VirtualSnapshot;
            }

            languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
        }
        else
        {
            Debug.Fail("Unexpected RazorLanguageKind. This shouldn't really happen in a real scenario.");
            return null;
        }

        if (!synchronized || virtualDocumentSnapshot is null)
        {
            return null;
        }

        var response = await _requestInvoker.ReinvokeRequestOnServerAsync<InlayHint, InlayHint>(
            virtualDocumentSnapshot.Snapshot.TextBuffer,
            Methods.TextDocumentInlayHintName,
            languageServerName,
            request.InlayHint,
            cancellationToken).ConfigureAwait(false);
        return response?.Response;
    }
}
