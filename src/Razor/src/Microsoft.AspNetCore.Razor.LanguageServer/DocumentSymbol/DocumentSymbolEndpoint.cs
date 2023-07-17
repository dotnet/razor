// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentSymbol;

[LanguageServerEndpoint(Methods.TextDocumentDocumentSymbolName)]
internal class DocumentSymbolEndpoint : IRazorRequestHandler<DocumentSymbolParams, SymbolInformation[]>, ICapabilitiesProvider
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

    public async Task<SymbolInformation[]> HandleRequestAsync(DocumentSymbolParams request, RazorRequestContext context, CancellationToken cancellationToken)
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
                Version = documentContext.Version,
            }
        );

        var symbolInformations = await _languageServer.SendRequestAsync<DelegatedDocumentSymbolParams, SymbolInformation[]?>(
            RazorLanguageServerCustomMessageTargets.RazorDocumentSymbolEndpoint,
            delegatedParams,
            cancellationToken).ConfigureAwait(false);

        if (symbolInformations is null)
        {
            return Array.Empty<SymbolInformation>();
        }

        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var csharpDocument = codeDocument.GetCSharpDocument();

        using var _ = ListPool<SymbolInformation>.GetPooledObject(out var mappedSymbols);

        foreach (var symbolInformation in symbolInformations)
        {
            if (_documentMappingService.TryMapToHostDocumentRange(csharpDocument, symbolInformation.Location.Range, out var newRange))
            {
                symbolInformation.Location.Range = newRange;
                symbolInformation.Location.Uri = documentContext.Uri;
                mappedSymbols.Add(symbolInformation);
            }
        }

        return mappedSymbols.ToArray();
    }
}
