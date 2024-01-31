// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.InlayHints;

[LanguageServerEndpoint(Methods.InlayHintResolveName)]
internal sealed class InlayHintResolveEndpoint(IRazorDocumentMappingService documentMappingService, IClientConnection clientConnection)
    : IRazorDocumentlessRequestHandler<InlayHint, InlayHint?>
{
    private readonly IRazorDocumentMappingService _documentMappingService = documentMappingService;
    private readonly IClientConnection _clientConnection = clientConnection ?? throw new ArgumentNullException(nameof(clientConnection));

    public bool MutatesSolutionState => false;

    public async Task<InlayHint?> HandleRequestAsync(InlayHint request, RazorRequestContext context, CancellationToken cancellationToken)
    {
        if (request.Data is not RazorInlayHintWrapper inlayHintWrapper)
        {
            return null;
        }

        var razorPosition = request.Position;
        request.Position = inlayHintWrapper.OriginalPosition;
        request.Data = inlayHintWrapper.OriginalData;

        // For now we only support C# inlay hints. Once Web Tools adds support we'll need to request from both servers and combine
        // the results, much like folding ranges.
        var delegatedRequest = new DelegatedInlayHintResolveParams(
            Identifier: inlayHintWrapper.TextDocument,
            InlayHint: request,
            ProjectedKind: RazorLanguageKind.CSharp
        );

        var inlayHint = await _clientConnection.SendRequestAsync<DelegatedInlayHintResolveParams, InlayHint?>(
            CustomMessageNames.RazorInlayHintResolveEndpoint,
            delegatedRequest,
            cancellationToken).ConfigureAwait(false);

        if (inlayHint is null)
        {
            return null;
        }

        Debug.Assert(request.Position == inlayHint.Position, "Resolving inlay hints should not change the position of them.");
        inlayHint.Position = razorPosition;

        return inlayHint;
    }
}
