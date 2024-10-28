// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.DocumentMapping;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Mapping;

[RazorLanguageServerEndpoint(LanguageServerConstants.RazorMapToDocumentEditsEndpoint)]
internal class RazorMapToDocumentEditsEndpoint(IDocumentMappingService documentMappingService, ITelemetryReporter telemetryReporter) :
    IRazorDocumentlessRequestHandler<RazorMapToDocumentEditsParams, RazorMapToDocumentEditsResponse?>,
    ITextDocumentIdentifierHandler<RazorMapToDocumentRangesParams, Uri>
{
    private readonly IDocumentMappingService _documentMappingService = documentMappingService;
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter;

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
                Edits = request.TextEdits,
                HostDocumentVersion = documentContext.Snapshot.Version,
            };
        }

        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        if (codeDocument is null || codeDocument.IsUnsupported())
        {
            return null;
        }

        using var builder = new PooledArrayBuilder<TextChange>();

        var razorSourceText = codeDocument.Source.Text;
        var csharpDocument = codeDocument.GetCSharpDocument();

        var originalText = csharpDocument.GetGeneratedSourceText();
        var edits = request.TextEdits;

        AddDirectlyMappedChanges(edits, codeDocument, ref builder.AsRef(), cancellationToken);

        var newText = originalText.WithChanges(edits);

        var oldUsings = await AddUsingsHelper.FindUsingDirectiveStringsAsync(
            originalText,
            cancellationToken).ConfigureAwait(false);

        var newUsings = await AddUsingsHelper.FindUsingDirectiveStringsAsync(
            newText,
            cancellationToken).ConfigureAwait(false);

        var removedUsings = oldUsings.Except(newUsings).ToHashSet();
        var addedUsings = newUsings.Except(oldUsings).ToArray();

        AddRemovedNamespaceEdits(removedUsings, codeDocument, ref builder.AsRef(), cancellationToken);

        var versionedIdentifier = new OptionalVersionedTextDocumentIdentifier() { Uri = documentContext.Uri, Version = documentContext.Snapshot.Version };
        AddNewNamespaceEdits(addedUsings, codeDocument, versionedIdentifier, ref builder.AsRef(), cancellationToken);

        return new RazorMapToDocumentEditsResponse()
        {
            Edits = NormalizeEdits(
                builder.DrainToImmutableOrderedBy(e => e.Span.Start),
                cancellationToken)
        };
    }

    private void AddDirectlyMappedChanges(TextChange[] edits, RazorCodeDocument codeDocument, ref PooledArrayBuilder<TextChange> builder, CancellationToken cancellationToken)
    {
        var root = codeDocument.GetSyntaxTree().Root;
        var razorText = codeDocument.Source.Text;
        var csharpDocument = codeDocument.GetCSharpDocument();
        var csharpText = csharpDocument.GetGeneratedSourceText();
        foreach (var edit in edits)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var linePositionSpan = csharpText.GetLinePositionSpan(edit.Span);

            if (!_documentMappingService.TryMapToHostDocumentRange(
                csharpDocument,
                linePositionSpan,
                MappingBehavior.Strict,
                out var mappedLinePositionSpan))
            {
                continue;
            }

            var mappedSpan = razorText.GetTextSpan(mappedLinePositionSpan);
            var node = root.FindNode(mappedSpan);
            if (node is null)
            {
                continue;
            }

            if (RazorSyntaxFacts.IsInUsingDirective(node))
            {
                continue;
            }

            var mappedEdit = new TextChange(mappedSpan, edit.NewText ?? "");
            builder.Add(mappedEdit);
        }
    }

    void AddRemovedNamespaceEdits(HashSet<string> removedUsings, RazorCodeDocument codeDocument, ref PooledArrayBuilder<TextChange> builder, CancellationToken cancellationToken)
    {

        var syntaxTreeRoot = codeDocument.GetSyntaxTree().Root;
        foreach (var node in syntaxTreeRoot.DescendantNodes())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (node is RazorDirectiveSyntax directiveNode)
            {
                var @namespace = RazorSyntaxFacts.TryGetNamespaceFromDirective(directiveNode);
                if (@namespace is null)
                {
                    continue;
                }

                if (!removedUsings.Contains(@namespace))
                {
                    continue;
                }

                builder.Add(new TextChange(node.FullSpan, string.Empty));
            }
        }
    }

    void AddNewNamespaceEdits(string[] addedUsings, RazorCodeDocument codeDocument, OptionalVersionedTextDocumentIdentifier versionedIdentifier, ref PooledArrayBuilder<TextChange> builder, CancellationToken cancellationToken)
    {
        foreach (var addedUsing in addedUsings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var edit = AddUsingsHelper.CreateAddUsingWorkspaceEdit(addedUsing, additionalEdit: null, codeDocument, versionedIdentifier);
            var sourceText = codeDocument.Source.Text;
            if (edit.DocumentChanges is { First.Length: > 0 } documentChanges)
            {
                var textEdits = documentChanges.SelectMany(static change => change.Edits);
                var textChanges = textEdits.Select(e => new TextChange(sourceText.GetTextSpan(e.Range), e.NewText));
                builder.AddRange(textChanges);
            }
        }
    }

    /// <summary>
    /// Go through edits and make sure a few things are true:
    ///
    /// <list type="number">
    /// <item>
    ///  No edit is added twice. This can happen if a rename happens.
    /// </item>
    /// <item>
    ///  No edit overlaps with another edit. If they do throw to capture logs but choose the first
    ///  edit to at least not completely fail. It's possible this will need to be tweaked later.
    /// </item>
    /// </list>
    /// </summary>

    private TextChange[] NormalizeEdits(ImmutableArray<TextChange> changes, CancellationToken cancellationToken)
    {
        using var normalizedEdits = new PooledArrayBuilder<TextChange>(changes.Length);

        // Ensure that the changes are sorted by start position otherwise
        // the normalization logic will not work.
        Debug.Assert(changes.SequenceEqual(changes.OrderBy(static c => c.Span.Start)));

        var droppedEdits = 0;
        for (var i = 0; i < changes.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (i == changes.Length - 1)
            {
                normalizedEdits.Add(changes[i]);
                break;
            }

            var edit = changes[i];
            var nextEdit = changes[i + 1];

            if (edit.Span.OverlapsWith(nextEdit.Span))
            {
                if (edit.Span.Contains(nextEdit.Span))
                {
                    // Add the edit that is contained in the other edit
                    // and skip the next edit.
                    normalizedEdits.Add(edit);
                    i++;
                    droppedEdits++;
                }
                else if (nextEdit.Span.Contains(edit.Span))
                {
                    // Add the edit that is contained in the other edit
                    // and skip the next edit.
                    normalizedEdits.Add(nextEdit);
                    i++;
                    droppedEdits++;
                }
                else if (edit.Span == nextEdit.Span)
                {
                    normalizedEdits.Add(nextEdit);
                    i++;

                    if (edit.NewText != nextEdit.NewText)
                    {
                        droppedEdits++;
                    }
                }
            }
            else
            {
                normalizedEdits.Add(edit);
            }
        }

        if (droppedEdits > 0)
        {
            _telemetryReporter.ReportFault(
                new DroppedEditsException(),
                "Potentially dropped edits when trying to map",
                new Property("droppedEditCount", droppedEdits));
        }

        return normalizedEdits.ToArray();
    }

    private sealed class DroppedEditsException : Exception
    {
    }
}
