﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    /// <summary>
    /// Top level type for LSP request handler.
    /// </summary>
    internal interface IRequestHandler
    {
    }

    internal interface IRequestHandler<RequestType, ResponseType> : IRequestHandler where RequestType : class
    {
        /// <summary>
        /// Handles an LSP request.
        /// </summary>
        /// <param name="request">the lsp request.</param>
        /// <param name="clientCapabilities">the client capabilities for the request.</param>
        /// <param name="cancellationToken">a cancellation token.</param>
        /// <returns>the LSP response.</returns>
        Task<ResponseType?> HandleRequestAsync(RequestType request, ClientCapabilities clientCapabilities, CancellationToken cancellationToken);
    }
}
