﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentHighlighting;

[RazorLanguageServerEndpoint(Methods.TextDocumentDocumentHighlightName)]
internal class DocumentHighlightEndpoint : AbstractRazorDelegatingEndpoint<DocumentHighlightParams, DocumentHighlight[]?>, ICapabilitiesProvider
{
    private readonly IDocumentMappingService _documentMappingService;

    public DocumentHighlightEndpoint(
        LanguageServerFeatureOptions languageServerFeatureOptions,
        IDocumentMappingService documentMappingService,
        IClientConnection clientConnection,
        ILoggerFactory loggerFactory)
        : base(languageServerFeatureOptions, documentMappingService, clientConnection, loggerFactory.GetOrCreateLogger<DocumentHighlightEndpoint>())
    {
        _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
    }

    public void ApplyCapabilities(VSInternalServerCapabilities serverCapabilities, VSInternalClientCapabilities clientCapabilities)
    {
        serverCapabilities.DocumentHighlightProvider = new DocumentHighlightOptions
        {
            WorkDoneProgress = false
        };
    }

    protected override string CustomMessageTarget => CustomMessageNames.RazorDocumentHighlightEndpointName;

    protected override Task<DocumentHighlight[]?> TryHandleAsync(DocumentHighlightParams request, RazorRequestContext requestContext, DocumentPositionInfo positionInfo, CancellationToken cancellationToken)
    {
        // We don't handle this in any particular way for Razor, we just delegate
        return SpecializedTasks.Null<DocumentHighlight[]>();
    }

    protected override Task<IDelegatedParams?> CreateDelegatedParamsAsync(DocumentHighlightParams request, RazorRequestContext requestContext, DocumentPositionInfo positionInfo, CancellationToken cancellationToken)
    {
        var documentContext = requestContext.DocumentContext;
        if (documentContext is null)
        {
            return SpecializedTasks.Null<IDelegatedParams>();
        }

        return Task.FromResult<IDelegatedParams?>(new DelegatedPositionParams(
            documentContext.GetTextDocumentIdentifierAndVersion(),
            positionInfo.Position,
            positionInfo.LanguageKind));
    }

    protected override async Task<DocumentHighlight[]?> HandleDelegatedResponseAsync(DocumentHighlight[]? response, DocumentHighlightParams request, RazorRequestContext requestContext, DocumentPositionInfo positionInfo, CancellationToken cancellationToken)
    {
        if (response is null)
        {
            return null;
        }

        var documentContext = requestContext.DocumentContext;
        if (documentContext is null)
        {
            return null;
        }

        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);

        var generatedDocument = codeDocument.GetGeneratedDocument(positionInfo.LanguageKind);
        foreach (var highlight in response)
        {
            if (_documentMappingService.TryMapToHostDocumentRange(generatedDocument, highlight.Range, out var mappedRange))
            {
                highlight.Range = mappedRange;
            }
        }

        return response;
    }
}
