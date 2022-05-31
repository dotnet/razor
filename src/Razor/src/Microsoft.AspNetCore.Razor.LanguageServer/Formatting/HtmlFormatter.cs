﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal class HtmlFormatter
    {
        private readonly DocumentVersionCache _documentVersionCache;
        private readonly ClientNotifierServiceBase _server;

        public HtmlFormatter(
            ClientNotifierServiceBase languageServer,
            DocumentVersionCache documentVersionCache)
        {
            _server = languageServer;
            _documentVersionCache = documentVersionCache;
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
                TextDocument = new TextDocumentIdentifier { Uri = FilePathNormalizer.Instance.Normalize(context.Uri) },
                Options = context.Options
            };

            var response = await _server.SendRequestAsync(LanguageServerConstants.RazorDocumentFormattingEndpoint, @params);
            var result = await response.Returning<RazorDocumentFormattingResponse>(cancellationToken);

            return result?.Edits ?? Array.Empty<TextEdit>();
        }

        public async Task<TextEdit[]> FormatOnTypeAsync(
           FormattingContext context,
           CancellationToken cancellationToken)
        {
            var documentVersion = await _documentVersionCache.TryGetDocumentVersionAsync(context.OriginalSnapshot, cancellationToken).ConfigureAwait(false);
            if (documentVersion == null)
            {
                return Array.Empty<TextEdit>();
            }

            context.SourceText.GetLineAndOffset(context.HostDocumentIndex, out var line, out var col);
            var @params = new RazorDocumentOnTypeFormattingParams()
            {
                Position = new Position(line, col),
                Character = context.TriggerCharacter.ToString(),
                TextDocument = new TextDocumentIdentifier { Uri = FilePathNormalizer.Instance.Normalize(context.Uri) },
                Options = context.Options,
                HostDocumentVersion = documentVersion.Value,
            };

            var response = await _server.SendRequestAsync(LanguageServerConstants.RazorDocumentOnTypeFormattingEndpoint, @params);
            var result = await response.Returning<RazorDocumentFormattingResponse>(cancellationToken);

            return result?.Edits ?? Array.Empty<TextEdit>();
        }
    }
}
