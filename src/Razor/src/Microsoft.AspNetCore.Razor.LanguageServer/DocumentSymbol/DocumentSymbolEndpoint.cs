// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentSymbol;

using DocumentSymbol = VisualStudio.LanguageServer.Protocol.DocumentSymbol;

[LanguageServerEndpoint(Methods.TextDocumentDocumentSymbolName)]
internal class DocumentSymbolEndpoint : IRazorRequestHandler<DocumentSymbolParams, SumType<DocumentSymbol[], SymbolInformation[]>>, IRegistrationExtension
{
    private readonly ClientNotifierServiceBase _languageServer;

    public DocumentSymbolEndpoint(ClientNotifierServiceBase languageServer)
    {
        _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));
    }

    public bool MutatesSolutionState => false;

    public RegistrationExtensionResult? GetRegistration(VSInternalClientCapabilities clientCapabilities)
    {
        var options = new DocumentSymbolOptions()
        {
            WorkDoneProgress = false
        };

        return new RegistrationExtensionResult("documentSymbolProvider", new SumType<bool, DocumentSymbolOptions>(options));
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

        return response ?? new SumType<DocumentSymbol[], SymbolInformation[]>(Array.Empty<DocumentSymbol>());
    }
}
