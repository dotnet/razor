﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.DocumentMapping;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Mapping;

[RazorLanguageServerEndpoint(LanguageServerConstants.RazorMapToDocumentRangesEndpoint)]
internal sealed class RazorMapToDocumentRangesEndpoint :
    IRazorDocumentlessRequestHandler<RazorMapToDocumentRangesParams, RazorMapToDocumentRangesResponse?>,
    ITextDocumentIdentifierHandler<RazorMapToDocumentRangesParams, Uri>
{
    private readonly IDocumentMappingService _documentMappingService;

    public RazorMapToDocumentRangesEndpoint(IDocumentMappingService documentMappingService)
    {
        _documentMappingService = documentMappingService;
    }

    public bool MutatesSolutionState { get; } = false;

    public Uri GetTextDocumentIdentifier(RazorMapToDocumentRangesParams request)
    {
        return request.RazorDocumentUri;
    }

    public async Task<RazorMapToDocumentRangesResponse?> HandleRequestAsync(RazorMapToDocumentRangesParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var documentContext = requestContext.DocumentContext;
        if (documentContext is null)
        {
            return null;
        }

        if (request.Kind != RazorLanguageKind.CSharp)
        {
            // All other non-C# requests map directly to where they are in the document.
            return new RazorMapToDocumentRangesResponse()
            {
                Ranges = request.ProjectedRanges,
                HostDocumentVersion = documentContext.Snapshot.Version,
            };
        }

        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var ranges = new Range[request.ProjectedRanges.Length];
        for (var i = 0; i < request.ProjectedRanges.Length; i++)
        {
            var projectedRange = request.ProjectedRanges[i];
            if (codeDocument.IsUnsupported() ||
                !_documentMappingService.TryMapToHostDocumentRange(codeDocument.GetCSharpDocument(), projectedRange, request.MappingBehavior, out var originalRange))
            {
                // All language queries on unsupported documents return Html. This is equivalent to what pre-VSCode Razor was capable of.
                ranges[i] = VsLspFactory.UndefinedRange;
                continue;
            }

            ranges[i] = originalRange;
        }

        return new RazorMapToDocumentRangesResponse()
        {
            Ranges = ranges,
            HostDocumentVersion = documentContext.Snapshot.Version,
        };
    }
}
