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

        public bool MutatesSolutionState => false;

        public RegistrationExtensionResult GetRegistration(VSInternalClientCapabilities clientCapabilities)
        {
            if (!_languageServerFeatureOptions.RegisterBuiltInFeatures)
            {
                return null;
            }

            const string ServerCapabilities = "colorProvider";
            var options = new SumType<bool, DocumentColorOptions>(new DocumentColorOptions());

            return new RegistrationExtensionResult(ServerCapabilities, options);
        }

        public TextDocumentIdentifier GetTextDocumentIdentifier(DocumentColorParams request)
        {
            return request.TextDocument;
        }

        public async Task<ColorInformation[]> HandleRequestAsync(DocumentColorParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
        {
            var documentColors = await _languageServer.SendRequestAsync<DocumentColorParams, ColorInformation[]>(
                RazorLanguageServerCustomMessageTargets.RazorProvideHtmlDocumentColorEndpoint,
                request,
                cancellationToken).ConfigureAwait(false);

            if (documentColors is null)
            {
                return Array.Empty<ColorInformation>();
            }

            // HTML and Razor documents have identical mapping locations. Because of this we can return the result as-is.
            return documentColors;
        }
    }
}
