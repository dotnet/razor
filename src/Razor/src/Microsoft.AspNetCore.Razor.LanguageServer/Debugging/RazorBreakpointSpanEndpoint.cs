﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Debugging
{
    internal class RazorBreakpointSpanEndpoint : IRazorBreakpointSpanEndpoint
    {
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly RazorDocumentMappingService _documentMappingService;
        private readonly ILogger _logger;

        public RazorBreakpointSpanEndpoint(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentResolver documentResolver,
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
            _documentMappingService = documentMappingService;
            _logger = loggerFactory.CreateLogger<RazorBreakpointSpanEndpoint>();
        }

        public async Task<RazorBreakpointSpanResponse?> Handle(RazorBreakpointSpanParamsBridge request, CancellationToken cancellationToken)
        {
            var documentSnapshot = await TryGetDocumentSnapshotAndVersionAsync(request.Uri.GetAbsoluteOrUNCPath(), cancellationToken).ConfigureAwait(false);
            if (documentSnapshot is null)
            {
                return null;
            }

            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync();
            var sourceText = await documentSnapshot.GetTextAsync();
            var linePosition = new LinePosition(request.Position.Line, request.Position.Character);
            var hostDocumentIndex = sourceText.Lines.GetPosition(linePosition);

            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            var projectedIndex = hostDocumentIndex;
            var languageKind = _documentMappingService.GetLanguageKind(codeDocument, hostDocumentIndex, rightAssociative: false);
            // If we're in C#, then map to the right position in the generated document
            if (languageKind == RazorLanguageKind.CSharp &&
                !_documentMappingService.TryMapToProjectedDocumentPosition(codeDocument, hostDocumentIndex, out _, out projectedIndex))
            {
                return null;
            }
            // Otherwise see if there is more C# on the line to map to
            else if (languageKind == RazorLanguageKind.Html &&
                !_documentMappingService.TryMapToProjectedDocumentOrNextCSharpPosition(codeDocument, hostDocumentIndex, out _, out projectedIndex))
            {
                return null;
            }
            else if (languageKind == RazorLanguageKind.Razor)
            {
                return null;
            }

            // Now ask Roslyn to adjust the breakpoint to a valid location in the code
            var csharpDocument = codeDocument.GetCSharpDocument();
            var syntaxTree = CSharpSyntaxTree.ParseText(csharpDocument.GeneratedCode, cancellationToken: cancellationToken);
            if (!RazorBreakpointSpans.TryGetBreakpointSpan(syntaxTree, projectedIndex, cancellationToken, out var csharpBreakpointSpan))
            {
                return null;
            }

            var csharpText = codeDocument.GetCSharpSourceText();

            csharpText.GetLineAndOffset(csharpBreakpointSpan.Start, out var startLineIndex, out var startCharacterIndex);
            csharpText.GetLineAndOffset(csharpBreakpointSpan.End, out var endLineIndex, out var endCharacterIndex);

            var projectedRange = new Range()
            {
                Start = new Position(startLineIndex, startCharacterIndex),
                End = new Position(endLineIndex, endCharacterIndex),
            };

            // Now map that new C# location back to the host document
            var mappingBehavior = GetMappingBehavior(documentSnapshot);
            if (!_documentMappingService.TryMapFromProjectedDocumentRange(codeDocument, projectedRange, mappingBehavior, out var hostDocumentRange))
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogTrace($"Breakpoint span request for ({request.Position.Line}, {request.Position.Character}) = ({hostDocumentRange.Start.Line}, {hostDocumentRange.Start.Character}");

            return new RazorBreakpointSpanResponse()
            {
                Range = hostDocumentRange
            };
        }

        // Internal for testing
        internal static MappingBehavior GetMappingBehavior(DocumentSnapshot snapshot)
        {
            if (snapshot.FileKind == FileKinds.Legacy)
            {
                // Razor files generate code in a "loosely" debuggable way. For instance if you were to do the following in a cshtml file:
                //
                //      @DateTime.Now
                //
                // This would render as:
                //
                //      #line 123 "C:/path/to/abc.cshtml"
                //      __o = DateTime.Now;
                //
                //      #line default
                //
                // This in turn results in a breakpoint span encompassing `|__o = DateTime.Now;|`. Problem is that if we're doing "strict" mapping
                // Razor only maps `DateTime.Now` so mapping would fail. Therefore in cshtml scenarios we fall back to inclusive mapping which allows
                // C# mappings that intersect to be acceptable mapping locations
                //
                // In Blazor this isn't an issue because the above renders as:
                //
                //      __o =
                //      #line 123 "C:/path/to/abc.razor"
                //      DateTime.Now
                //
                //      #line default
                //      ;
                //
                // Which results in a proper mapping

                return MappingBehavior.Inclusive;
            }

            return MappingBehavior.Strict;
        }

        private Task<DocumentSnapshot?> TryGetDocumentSnapshotAndVersionAsync(string uri, CancellationToken cancellationToken)
        {
            return _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync<DocumentSnapshot?>(() =>
            {
                if (_documentResolver.TryResolveDocument(uri, out var documentSnapshot))
                {
                    return documentSnapshot;
                }

                return null;
            }, cancellationToken);
        }
    }
}
