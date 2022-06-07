// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts.Debugging;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Debugging
{
    internal class RazorProximityExpressionsEndpoint : IRazorProximityExpressionsEndpoint
    {
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly RazorDocumentMappingService _documentMappingService;
        private readonly ILogger _logger;

        public RazorProximityExpressionsEndpoint(
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

        public async Task<RazorProximityExpressionsResponse?> Handle(RazorProximityExpressionsParamsBridge request, CancellationToken cancellationToken)
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
            var expressions = RazorCSharpProximityExpressionResolverService.GetProximityExpressions(syntaxTree, projectedIndex, cancellationToken)?.ToList();
            if (expressions == null)
            {
                return null;
            }

            _logger.LogTrace("Proximity expressions request for ({Line}, {Character}) yielded {expressionsCount} results.", 
                request.Position.Line, request.Position.Character, expressions.Count);

            return new RazorProximityExpressionsResponse
            {
                Expressions = expressions,
            };
        }

        private Task<DocumentSnapshot?> TryGetDocumentSnapshotAndVersionAsync(string uri, CancellationToken cancellationToken)
        {
            return _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
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
