// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class RazorLanguageEndpoint :
        IRazorLanguageQueryHandler,
        IRazorMapToDocumentRangesHandler,
        IRazorMapToDocumentEditsHandler
    {
        private readonly DocumentContextFactory _documentContextFactory;
        private readonly RazorDocumentMappingService _documentMappingService;
        private readonly RazorFormattingService _razorFormattingService;
        private readonly ILogger _logger;

        public RazorLanguageEndpoint(
            DocumentContextFactory documentContextFactory,
            RazorDocumentMappingService documentMappingService,
            RazorFormattingService razorFormattingService,
            ILoggerFactory loggerFactory)
        {
            if (documentContextFactory is null)
            {
                throw new ArgumentNullException(nameof(documentContextFactory));
            }

            if (documentMappingService is null)
            {
                throw new ArgumentNullException(nameof(documentMappingService));
            }

            if (razorFormattingService is null)
            {
                throw new ArgumentNullException(nameof(razorFormattingService));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _documentContextFactory = documentContextFactory;
            _documentMappingService = documentMappingService;
            _razorFormattingService = razorFormattingService;
            _logger = loggerFactory.CreateLogger<RazorLanguageEndpoint>();
        }

        public async Task<RazorLanguageQueryResponse> Handle(RazorLanguageQueryParams request, CancellationToken cancellationToken)
        {
            var documentUri = request.Uri.GetAbsoluteOrUNCPath();
            var documentContext = await _documentContextFactory.TryCreateAsync(request.Uri, cancellationToken).ConfigureAwait(false);
            if (documentContext is null)
            {
                _logger.LogError("Failed to get the document snapshot '{documentUri}', could not map to document ranges.", documentUri);
                throw new InvalidOperationException($"Unable to resolve document {request.Uri.GetAbsoluteOrUNCPath()}.");
            }

            var documentSnapshot = documentContext.Snapshot;
            var documentVersion = documentContext.Version;

            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync();
            var sourceText = await documentSnapshot.GetTextAsync();
            var linePosition = new LinePosition(request.Position.Line, request.Position.Character);
            var hostDocumentIndex = sourceText.Lines.GetPosition(linePosition);
            var responsePosition = request.Position;

            if (codeDocument.IsUnsupported())
            {
                // All language queries on unsupported documents return Html. This is equivalent to what pre-VSCode Razor was capable of.
                return new RazorLanguageQueryResponse()
                {
                    Kind = RazorLanguageKind.Html,
                    Position = responsePosition,
                    PositionIndex = hostDocumentIndex,
                    HostDocumentVersion = documentVersion,
                };
            }

            var responsePositionIndex = hostDocumentIndex;

            var languageKind = _documentMappingService.GetLanguageKind(codeDocument, hostDocumentIndex, rightAssociative: false);
            if (languageKind == RazorLanguageKind.CSharp)
            {
                if (_documentMappingService.TryMapToProjectedDocumentPosition(codeDocument, hostDocumentIndex, out var projectedPosition, out var projectedIndex))
                {
                    // For C# locations, we attempt to return the corresponding position
                    // within the projected document
                    responsePosition = projectedPosition;
                    responsePositionIndex = projectedIndex;
                }
                else
                {
                    // It no longer makes sense to think of this location as C#, since it doesn't
                    // correspond to any position in the projected document. This should not happen
                    // since there should be source mappings for all the C# spans.
                    languageKind = RazorLanguageKind.Razor;
                    responsePositionIndex = hostDocumentIndex;
                }
            }

            _logger.LogTrace("Language query request for ({requestPositionLine}, {requestPositionCharacter}) = {languageKind} at ({responsePositionLine}, {responsePositionCharacter})",
                request.Position.Line, request.Position.Character, languageKind, responsePosition.Line, responsePosition.Character);

            return new RazorLanguageQueryResponse()
            {
                Kind = languageKind,
                Position = responsePosition,
                PositionIndex = responsePositionIndex,
                HostDocumentVersion = documentVersion
            };
        }

        public async Task<RazorMapToDocumentRangesResponse?> Handle(RazorMapToDocumentRangesParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var documentContext = await _documentContextFactory.TryCreateAsync(request.RazorDocumentUri, cancellationToken).ConfigureAwait(false);
            if (documentContext is null)
            {
                // Document requested without prior knowledge
                return null;
            }

            if (request.Kind != RazorLanguageKind.CSharp)
            {
                // All other non-C# requests map directly to where they are in the document.
                return new RazorMapToDocumentRangesResponse()
                {
                    Ranges = request.ProjectedRanges,
                    HostDocumentVersion = documentContext.Version,
                };
            }

            var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken);
            var ranges = new Range[request.ProjectedRanges.Length];
            for (var i = 0; i < request.ProjectedRanges.Length; i++)
            {
                var projectedRange = request.ProjectedRanges[i];
                if (codeDocument.IsUnsupported() ||
                    !_documentMappingService.TryMapFromProjectedDocumentRange(codeDocument, projectedRange, request.MappingBehavior, out var originalRange))
                {
                    // All language queries on unsupported documents return Html. This is equivalent to what pre-VSCode Razor was capable of.
                    ranges[i] = RangeExtensions.UndefinedRange;
                    continue;
                }

                ranges[i] = originalRange;
            }

            return new RazorMapToDocumentRangesResponse()
            {
                Ranges = ranges,
                HostDocumentVersion = documentContext.Version,
            };
        }

        public async Task<RazorMapToDocumentEditsResponse> Handle(RazorMapToDocumentEditsParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var documentContext = await _documentContextFactory.TryCreateAsync(request.RazorDocumentUri, cancellationToken).ConfigureAwait(false);
            if (documentContext is null)
            {
                throw new InvalidOperationException($"Unable to resolve document {request.RazorDocumentUri.GetAbsoluteOrUNCPath()}.");
            }

            var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken);
            if (codeDocument.IsUnsupported())
            {
                return new RazorMapToDocumentEditsResponse()
                {
                    TextEdits = Array.Empty<TextEdit>(),
                    HostDocumentVersion = documentContext.Version
                };
            }

            if (request.TextEditKind == TextEditKind.FormatOnType)
            {
                var mappedEdits = await _razorFormattingService.FormatOnTypeAsync(request.RazorDocumentUri, documentContext.Snapshot, request.Kind, request.ProjectedTextEdits, request.FormattingOptions, hostDocumentIndex: 0, triggerCharacter: '\0', cancellationToken);

                return new RazorMapToDocumentEditsResponse()
                {
                    TextEdits = mappedEdits,
                    HostDocumentVersion = documentContext.Version,
                };
            }
            else if (request.TextEditKind == TextEditKind.Snippet)
            {
                var mappedEdits = await _razorFormattingService.FormatSnippetAsync(request.RazorDocumentUri, documentContext.Snapshot, request.Kind, request.ProjectedTextEdits, request.FormattingOptions, cancellationToken);

                return new RazorMapToDocumentEditsResponse()
                {
                    TextEdits = mappedEdits,
                    HostDocumentVersion = documentContext.Version,
                };
            }

            if (request.Kind != RazorLanguageKind.CSharp)
            {
                // All other non-C# requests map directly to where they are in the document.
                return new RazorMapToDocumentEditsResponse()
                {
                    TextEdits = request.ProjectedTextEdits,
                    HostDocumentVersion = documentContext.Version,
                };
            }

            var edits = new List<TextEdit>();
            for (var i = 0; i < request.ProjectedTextEdits.Length; i++)
            {
                var projectedRange = request.ProjectedTextEdits[i].Range;
                if (!_documentMappingService.TryMapFromProjectedDocumentRange(codeDocument, projectedRange, out var originalRange))
                {
                    // Can't map range. Discard this edit.
                    continue;
                }

                var edit = new TextEdit()
                {
                    Range = originalRange,
                    NewText = request.ProjectedTextEdits[i].NewText
                };

                edits.Add(edit);
            }

            return new RazorMapToDocumentEditsResponse()
            {
                TextEdits = edits.ToArray(),
                HostDocumentVersion = documentContext.Version,
            };
        }
    }
}
