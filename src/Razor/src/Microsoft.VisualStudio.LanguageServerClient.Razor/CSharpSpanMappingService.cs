// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using System.Collections.Immutable;
using System.Collections.Generic;
using Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    internal sealed class CSharpSpanMappingService : IRazorSpanMappingService
    {
        private readonly LSPDocumentMappingProvider _lspDocumentMappingProvider;

        private readonly ITextSnapshot _textSnapshot;
        private readonly LSPDocumentSnapshot _documentSnapshot;

        public CSharpSpanMappingService(
            LSPDocumentMappingProvider lspDocumentMappingProvider,
            LSPDocumentSnapshot documentSnapshot,
            ITextSnapshot textSnapshot)
        {
            if (lspDocumentMappingProvider is null)
            {
                throw new ArgumentNullException(nameof(lspDocumentMappingProvider));
            }

            if (textSnapshot == null)
            {
                throw new ArgumentNullException(nameof(textSnapshot));
            }

            if (documentSnapshot is null)
            {
                throw new ArgumentNullException(nameof(documentSnapshot));
            }

            _lspDocumentMappingProvider = lspDocumentMappingProvider;

            _textSnapshot = textSnapshot;
            _documentSnapshot = documentSnapshot;
        }

        public async Task<ImmutableArray<RazorMappedSpanResult>> MapSpansAsync(
            Document document,
            IEnumerable<TextSpan> spans,
            CancellationToken cancellationToken)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (spans == null)
            {
                throw new ArgumentNullException(nameof(spans));
            }

            // Called on an uninitialized snapshot.
            if (_textSnapshot == null || _documentSnapshot == null)
            {
                return ImmutableArray.Create<RazorMappedSpanResult>();
            }

            var sourceText = _textSnapshot.AsText();
            var projectedRanges = spans.Select(span => {
                var range = span.AsRange(sourceText);
                return new Range()
                {
                    Start = new Position((int)range.Start.Line, (int)range.Start.Character),
                    End = new Position((int)range.End.Line, (int)range.End.Character)
                };
            }).ToArray();

            var mappedResult = await _lspDocumentMappingProvider.MapToDocumentRangesAsync(
                RazorLanguageKind.CSharp,
                _documentSnapshot.Uri,
                projectedRanges,
                cancellationToken).ConfigureAwait(false);

            var results = ImmutableArray.CreateBuilder<RazorMappedSpanResult>();
            foreach (var mappedRange in mappedResult.Ranges)
            {
                var mappedSpan = mappedRange.AsTextSpan(sourceText);
                var linePositionSpan = sourceText.Lines.GetLinePositionSpan(mappedSpan);
                var fileName = Path.GetFileName(_documentSnapshot.Uri.LocalPath);
                results.Add(new RazorMappedSpanResult(fileName, linePositionSpan, mappedSpan));
            }

            return results.ToImmutable();
        }
    }
}
