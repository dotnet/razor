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

        // Per the LSP spec, there are no special capabilities or options for textDocument/colorPresentation
        // since it is sent as a resolve request for textDocument/documentColor
        public RegistrationExtensionResult? GetRegistration(VSInternalClientCapabilities clientCapabilities)
        {
            return null;
        }

        public async Task<ColorPresentation[]> Handle(ColorPresentationParamsBridge request, CancellationToken cancellationToken)
        {
            var delegatedRequest = await _languageServer.SendRequestAsync(
                RazorLanguageServerCustomMessageTargets.RazorProvideHtmlColorPresentationEndpoint, request).ConfigureAwait(false);
            var colorPresentation = await delegatedRequest.Returning<ColorPresentation[]>(cancellationToken).ConfigureAwait(false);

            if (colorPresentation is null)
            {
                return Array.Empty<ColorPresentation>();
            }

            // HTML and Razor documents have identical mapping locations. Because of this we can return the result as-is.

            return colorPresentation;
        }
    }
}
