// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentColor
{
    internal class DocumentColorEndpoint : IDocumentColorEndpoint
    {
        private readonly ClientNotifierServiceBase _languageServer;
        private readonly LanguageServerFeatureOptions _languageServerFeatureOptions;

        public DocumentColorEndpoint(ClientNotifierServiceBase languageServer, LanguageServerFeatureOptions languageServerFeatureOptions)
        {
            if (languageServer is null)
            {
                throw new ArgumentNullException(nameof(languageServer));
            }

            _languageServer = languageServer;
            _languageServerFeatureOptions = languageServerFeatureOptions;
        }

        public RegistrationExtensionResult? GetRegistration(VSInternalClientCapabilities clientCapabilities)
        {
            if (_languageServerFeatureOptions.RegisterBuiltInFeatures)
            {
                return null;
            }

            const string ServerCapabilities = "colorProvider";
            var options = new DocumentColorOptions();

            return new RegistrationExtensionResult(ServerCapabilities, options);
        }

        public async Task<ColorInformation[]> Handle(DocumentColorParamsBridge request, CancellationToken cancellationToken)
        {
            var delegatedRequest = await _languageServer.SendRequestAsync(RazorLanguageServerCustomMessageTargets.RazorProvideHtmlDocumentColorEndpoint, request).ConfigureAwait(false);
            var documentColors = await delegatedRequest.Returning<ColorInformation[]>(cancellationToken).ConfigureAwait(false);

            if (documentColors is null)
            {
                return Array.Empty<ColorInformation>();
            }

            // HTML and Razor documents have identical mapping locations. Because of this we can return the result as-is.

            return documentColors;
        }
    }
}
