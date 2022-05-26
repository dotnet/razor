// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentColor
{
    internal class DocumentColorEndpoint : IDocumentColorHandler
    {
        private static readonly Container<ColorInformation> EmptyDocumentColors = new Container<ColorInformation>();
        private readonly ClientNotifierServiceBase _languageServer;

        public DocumentColorEndpoint(ClientNotifierServiceBase languageServer)
        {
            if (languageServer is null)
            {
                throw new ArgumentNullException(nameof(languageServer));
            }

            _languageServer = languageServer;
        }
        public DocumentColorRegistrationOptions GetRegistrationOptions(ColorProviderCapability? capability, ClientCapabilities clientCapabilities)
        {
            return new DocumentColorRegistrationOptions()
            {
                DocumentSelector = RazorDefaults.Selector,
            };
        }

        public async Task<Container<ColorInformation>> Handle(DocumentColorParams request, CancellationToken cancellationToken)
        {
            var delegatedRequest = await _languageServer.SendRequestAsync(LanguageServerConstants.RazorProvideHtmlDocumentColorEndpoint, request).ConfigureAwait(false);
            var documentColors = await delegatedRequest.Returning<Container<ColorInformation>?>(cancellationToken).ConfigureAwait(false);

            if (documentColors is null)
            {
                return EmptyDocumentColors;
            }

            // HTML and Razor documents have identical mapping locations. Because of this we can return the result as-is.

            return documentColors;
        }
    }
}
