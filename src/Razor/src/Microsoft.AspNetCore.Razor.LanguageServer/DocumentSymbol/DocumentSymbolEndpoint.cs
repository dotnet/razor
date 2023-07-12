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
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentSymbol;

using DocumentSymbol = VisualStudio.LanguageServer.Protocol.DocumentSymbol;

[LanguageServerEndpoint(Methods.TextDocumentDocumentSymbolName)]
internal class DocumentSymbolEndpoint : IRazorRequestHandler<DocumentSymbolParams, SumType<DocumentSymbol[], SymbolInformation[]>>, ICapabilitiesProvider
{
    private readonly ClientNotifierServiceBase _languageServer;
    private readonly IRazorDocumentMappingService _documentMappingService;

    public DocumentSymbolEndpoint(ClientNotifierServiceBase languageServer, IRazorDocumentMappingService documentMappingService)
    {
        _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));
        _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
    }

    public bool MutatesSolutionState => false;

    public void ApplyCapabilities(VSInternalServerCapabilities serverCapabilities, VSInternalClientCapabilities clientCapabilities)
    {
        serverCapabilities.DocumentSymbolProvider = new DocumentSymbolOptions()
        {
            WorkDoneProgress = false
        };
    }

    public TextDocumentIdentifier GetTextDocumentIdentifier(DocumentSymbolParams request)
        => request.TextDocument;

    public async Task<SumType<DocumentSymbol[], SymbolInformation[]>> HandleRequestAsync(DocumentSymbolParams request, RazorRequestContext context, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var documentContext = context.GetRequiredDocumentContext();
        var delegatedParams = new DelegatedDocumentSymbolParams(
            new VersionedTextDocumentIdentifier()
            {
                Uri = documentContext.Uri,
                Version = documentContext.Version
            },
            request.TextDocument.GetProjectContext()
        );

        var response = await _languageServer.SendRequestAsync<DelegatedDocumentSymbolParams, SumType<DocumentSymbol[], SymbolInformation[]>?>(
            RazorLanguageServerCustomMessageTargets.RazorDocumentSymbolEndpoint,
            delegatedParams,
            cancellationToken).ConfigureAwait(false);

        if (!response.HasValue)
        {
            return new();
        }

        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var csharpDocument = codeDocument.GetCSharpDocument();

        if (response.Value.TryGetFirst(out var documentSymbols))
        {
            foreach (var documentSymbol in documentSymbols)
            {
                if (_documentMappingService.TryMapToGeneratedDocumentRange(csharpDocument, documentSymbol.Range, out var newRange))
                {
                    documentSymbol.Range = newRange;
                }
            }

            return documentSymbols;
        }
        else
        {
            var symbolInformations = response.Value.Second;

            foreach (var symbolInformation in symbolInformations)
            {
                if (_documentMappingService.TryMapToGeneratedDocumentRange(csharpDocument, symbolInformation.Location.Range, out var newRange))
                {
                    symbolInformation.Location.Range = newRange;
                }
            }

            return symbolInformations;
        }
    }
}
