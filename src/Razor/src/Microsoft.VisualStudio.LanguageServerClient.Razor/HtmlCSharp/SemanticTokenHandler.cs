// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [ExportLspMethod(LanguageServerConstants.RazorSemanticTokensEndpoint)]
    internal class SemanticTokenHandler : IRequestHandler<SemanticTokensParams, SemanticTokens>
    {
        private readonly LSPRequestInvoker _requestInvoker;

        [ImportingConstructor]
        public SemanticTokenHandler(LSPRequestInvoker requestInvoker)
        {
            if (requestInvoker is null)
            {
                throw new ArgumentNullException(nameof(requestInvoker));
            }

            _requestInvoker = requestInvoker;
        }

        public async Task<SemanticTokens> HandleRequestAsync(SemanticTokensParams request, ClientCapabilities clientCapabilities, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (clientCapabilities is null)
            {
                throw new ArgumentNullException(nameof(clientCapabilities));
            }

            var result = await _requestInvoker.ReinvokeRequestOnServerAsync<SemanticTokensParams, SemanticTokens>(
                LanguageServerConstants.RazorSemanticTokensEndpoint,
                LanguageServerKind.Razor,
                request,
                cancellationToken).ConfigureAwait(false);

            return result;
        }
    }
}
