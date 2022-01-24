// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Debugging
{
    internal class RazorBreakpointSpanEndpoint : IRazorBreakpointSpanHandler
    {
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly DocumentVersionCache _documentVersionCache;
        private readonly RazorDocumentMappingService _documentMappingService;
        private readonly ILogger _logger;

        public RazorBreakpointSpanEndpoint(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentResolver documentResolver,
            DocumentVersionCache documentVersionCache,
            RazorDocumentMappingService documentMappingService,
            ILoggerFactory loggerFactory)
        {
            if (projectSnapshotManagerDispatcher is null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            }

            if (documentResolver is null)
            {
                throw new ArgumentNullException(nameof(documentResolver));
            }

            if (documentVersionCache is null)
            {
                throw new ArgumentNullException(nameof(documentVersionCache));
            }

            if (documentMappingService is null)
            {
                throw new ArgumentNullException(nameof(documentMappingService));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _documentResolver = documentResolver;
            _documentVersionCache = documentVersionCache;
            _documentMappingService = documentMappingService;
            _logger = loggerFactory.CreateLogger<RazorBreakpointSpanEndpoint>();
        }

        public async Task<RazorBreakpointSpanResponse> Handle(RazorBreakpointSpanParams request, CancellationToken cancellationToken)
        {
            var info = await TryGetDocumentSnapshotAndVersionAsync(request.Uri.GetAbsoluteOrUNCPath(), cancellationToken).ConfigureAwait(false);

            Debug.Assert(info != null, "Failed to get the document snapshot, could not map to document ranges.");

            if (info is null)
            {
                throw new InvalidOperationException($"Unable to resolve document {request.Uri.GetAbsoluteOrUNCPath()}.");
            }

            var (documentSnapshot, documentVersion) = info;

            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync();
            var sourceText = await documentSnapshot.GetTextAsync();
            var linePosition = new LinePosition(request.Position.Line, request.Position.Character);
            var hostDocumentIndex = sourceText.Lines.GetPosition(linePosition);
            var responsePosition = request.Position;

            if (codeDocument.IsUnsupported())
            {
                // All language queries on unsupported documents return Html. This is equivalent to what pre-VSCode Razor was capable of.
                return new RazorBreakpointSpanResponse()
                {
                    Kind = RazorLanguageKind.Html,
                    Position = responsePosition,
                    PositionIndex = hostDocumentIndex,
                    HostDocumentVersion = documentVersion,
                };
            }

            var responsePositionIndex = hostDocumentIndex;

            var languageKind = _documentMappingService.GetLanguageKind(codeDocument, hostDocumentIndex);
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
            else if (languageKind == RazorLanguageKind.Html)
            {
                if (_documentMappingService.TryMapToProjectedDocumentOrNextCSharpPosition(codeDocument, hostDocumentIndex, out var projectedPosition, out var projectedIndex))
                {
                    languageKind = RazorLanguageKind.CSharp;
                    responsePosition = projectedPosition;
                    responsePositionIndex = projectedIndex;
                }
            }

            _logger.LogTrace($"Breakpoint span request for ({request.Position.Line}, {request.Position.Character}) = {languageKind} at ({responsePosition.Line}, {responsePosition.Character}");

            return new RazorBreakpointSpanResponse()
            {
                Kind = languageKind,
                Position = responsePosition,
                PositionIndex = responsePositionIndex,
                HostDocumentVersion = documentVersion
            };
        }

        private Task<DocumentSnapshotAndVersion?> TryGetDocumentSnapshotAndVersionAsync(string uri, CancellationToken cancellationToken)
        {
            return _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync<DocumentSnapshotAndVersion?>(() =>
            {
                if (_documentResolver.TryResolveDocument(uri, out var documentSnapshot))
                {
                    if (_documentVersionCache.TryGetDocumentVersion(documentSnapshot, out var version))
                    {
                        return new DocumentSnapshotAndVersion(documentSnapshot, version.Value);
                    }
                }

                return null;
            }, cancellationToken);
        }

        private record DocumentSnapshotAndVersion(DocumentSnapshot Snapshot, int Version);
    }
}
