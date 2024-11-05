// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.DocumentMapping;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Mapping;

[RazorLanguageServerEndpoint(LanguageServerConstants.RazorMapToDocumentEditsEndpoint)]
internal partial class RazorMapToDocumentEditsEndpoint(IDocumentMappingService documentMappingService, ITelemetryReporter telemetryReporter, ILoggerFactory loggerFactory) :
    IRazorDocumentlessRequestHandler<RazorMapToDocumentEditsParams, RazorMapToDocumentEditsResponse?>,
    ITextDocumentIdentifierHandler<RazorMapToDocumentRangesParams, Uri>
{
    private readonly IDocumentMappingService _documentMappingService = documentMappingService;
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<RazorMapToDocumentEditsEndpoint>();

    public bool MutatesSolutionState => false;

    public Uri GetTextDocumentIdentifier(RazorMapToDocumentRangesParams request)
    {
        return request.RazorDocumentUri;
    }

    public async Task<RazorMapToDocumentEditsResponse?> HandleRequestAsync(RazorMapToDocumentEditsParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        var documentContext = requestContext.DocumentContext;
        if (documentContext is null)
        {
            return null;
        }

        if (request.TextEdits.Length == 0)
        {
            return null;
        }

        if (request.Kind != RazorLanguageKind.CSharp)
        {
            // All other non-C# requests map directly to where they are in the document,
            // so the edits do as well
            return new RazorMapToDocumentEditsResponse()
            {
                TextEdits = request.TextEdits,
                HostDocumentVersion = documentContext.Snapshot.Version,
            };
        }

        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        if (codeDocument.IsUnsupported())
        {
            return null;
        }

        var mappedEdits = await RazorEditHelper.MapCSharpEditsAsync(
            request.TextEdits.ToImmutableArray(),
            documentContext.Snapshot,
            codeDocument,
            _documentMappingService,
            _telemetryReporter,
            cancellationToken).ConfigureAwait(false);

        _logger.LogTrace($"""
            Before:
            {DisplayEdits(request.TextEdits)}

            After:
            {DisplayEdits(mappedEdits)}
            """);

        return new RazorMapToDocumentEditsResponse()
        {
            TextEdits = mappedEdits.ToArray(),
        };
    }

    private string DisplayEdits(IEnumerable<TextEdit> edits)
        => string.Join(
            Environment.NewLine,
            edits.Select(e => $"{e.Range} => '{e.NewText}'"));
}
