// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ColorPresentation
{
    internal class ColorPresentationEndpoint : IColorPresentationEndpoint
    {
        public const string ColorPresentationMethodName = "textDocument/colorPresentation";

        private readonly ClientNotifierServiceBase _languageServer;

        public ColorPresentationEndpoint(ClientNotifierServiceBase languageServer)
        {
            if (languageServer is null)
            {
                throw new ArgumentNullException(nameof(languageServer));
            }

            _languageServer = languageServer;
        }

        public bool MutatesSolutionState => false;

        public TextDocumentIdentifier GetTextDocumentIdentifier(ColorPresentationParams request) => request.TextDocument;

        public async Task<ColorPresentation[]> HandleRequestAsync(ColorPresentationParams request, RazorRequestContext context, CancellationToken cancellationToken)
        {
            var colorPresentations = await _languageServer.SendRequestAsync<ColorPresentationParams, ColorPresentation[]>(
                RazorLanguageServerCustomMessageTargets.RazorProvideHtmlColorPresentationEndpoint,
                request,
                cancellationToken).ConfigureAwait(false);

            if (colorPresentations is null)
            {
                return Array.Empty<ColorPresentation>();
            }

            // HTML and Razor documents have identical mapping locations. Because of this we can return the result as-is.
            return colorPresentations;
        }
    }
}
