// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal class HtmlFormatter
    {
        private readonly FilePathNormalizer _filePathNormalizer;
        private readonly ClientNotifierServiceBase _server;

        public HtmlFormatter(
            ClientNotifierServiceBase languageServer,
            FilePathNormalizer filePathNormalizer)
        {
            if (languageServer is null)
            {
                throw new ArgumentNullException(nameof(languageServer));
            }

            if (filePathNormalizer is null)
            {
                throw new ArgumentNullException(nameof(filePathNormalizer));
            }

            _server = languageServer;
            _filePathNormalizer = filePathNormalizer;
        }

        public async Task<TextEdit[]> FormatAsync(
            FormattingContext context,
            CancellationToken cancellationToken)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var @params = new DocumentFormattingParams()
            {
                TextDocument = new TextDocumentIdentifier { Uri = _filePathNormalizer.Normalize(context.Uri.GetAbsoluteOrUNCPath()) },
                Options = context.Options
            };

            var response = await _server.SendRequestAsync(LanguageServerConstants.RazorDocumentFormattingEndpoint, @params);
            var result = await response.Returning<RazorDocumentFormattingResponse>(cancellationToken);

            return result?.Edits ?? Array.Empty<TextEdit>();
        }
    }
}
