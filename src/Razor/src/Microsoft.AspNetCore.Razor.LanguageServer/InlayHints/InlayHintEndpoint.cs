// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.InlayHints;

[LanguageServerEndpoint(Methods.TextDocumentInlayHintName)]
internal sealed class InlayHintEndpoint(LanguageServerFeatureOptions featureOptions, IRazorDocumentMappingService documentMappingService, IClientConnection clientConnection)
    : IRazorRequestHandler<InlayHintParams, InlayHint[]?>, ICapabilitiesProvider
{
    private readonly LanguageServerFeatureOptions _featureOptions = featureOptions;
    private readonly IRazorDocumentMappingService _documentMappingService = documentMappingService;
    private readonly IClientConnection _clientConnection = clientConnection ?? throw new ArgumentNullException(nameof(clientConnection));

    public bool MutatesSolutionState => false;

    public void ApplyCapabilities(VSInternalServerCapabilities serverCapabilities, VSInternalClientCapabilities clientCapabilities)
    {
        // Not supported in VS Code
        if (!_featureOptions.SingleServerSupport)
        {
            return;
        }

        serverCapabilities.EnableInlayHints();
    }

    public TextDocumentIdentifier GetTextDocumentIdentifier(InlayHintParams request)
        => request.TextDocument;

    public async Task<InlayHint[]?> HandleRequestAsync(InlayHintParams request, RazorRequestContext context, CancellationToken cancellationToken)
    {
        var documentContext = context.GetRequiredDocumentContext();
        return await GetInlayHintsAsync(documentContext, request.Range, cancellationToken).ConfigureAwait(false);
    }

    private async Task<InlayHint[]?> GetInlayHintsAsync(VersionedDocumentContext documentContext, Range range, CancellationToken cancellationToken)
    {
        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var csharpDocument = codeDocument.GetCSharpDocument();

        // We are given a range by the client, but our mapping only succeeds if the start and end of the range can both be mapped
        // to C#. Since that doesn't logically match what we want from inlay hints, we instead get the minimum range of mappable
        // C# to get hints for. We'll filter that later, to remove the sections that can't be mapped back.
        if (!_documentMappingService.TryMapToGeneratedDocumentRange(csharpDocument, range, out var projectedRange) &&
            !RazorSemanticTokensInfoService.TryGetMinimalCSharpRange(codeDocument, range, out projectedRange))
        {
            // There's no C# in the range.
            return null;
        }

        // For now we only support C# inlay hints. Once Web Tools adds support we'll need to request from both servers and combine
        // the results, much like folding ranges.
        var delegatedRequest = new DelegatedInlayHintParams(
            Identifier: documentContext.Identifier,
            ProjectedRange: projectedRange,
            ProjectedKind: RazorLanguageKind.CSharp
        );

        var inlayHints = await _clientConnection.SendRequestAsync<DelegatedInlayHintParams, InlayHint[]?>(
            CustomMessageNames.RazorInlayHintEndpoint,
            delegatedRequest,
            cancellationToken).ConfigureAwait(false);

        if (inlayHints is null)
        {
            return null;
        }

        var csharpSourceText = codeDocument.GetCSharpSourceText();
        using var _1 = ArrayBuilderPool<InlayHint>.GetPooledObject(out var inlayHintsBuilder);
        foreach (var hint in inlayHints)
        {
            if (hint.Position.TryGetAbsoluteIndex(csharpSourceText, null, out var absoluteIndex) &&
                _documentMappingService.TryMapToHostDocumentPosition(csharpDocument, absoluteIndex, out Position? hostDocumentPosition, out _))
            {
                hint.Data = new RazorInlayHintWrapper
                {
                    TextDocument = documentContext.Identifier,
                    OriginalData = hint.Data,
                    OriginalPosition = hint.Position
                };
                hint.Position = hostDocumentPosition;
                inlayHintsBuilder.Add(hint);
            }
        }

        return inlayHintsBuilder.ToArray();
    }
}
