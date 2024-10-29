// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
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
using Microsoft.AspNetCore.Razor.Utilities;
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
        if (codeDocument.IsUnsupported())
        {
            return null;
        }

        using var builder = new PooledArrayBuilder<TextChange>();

        var edits = request.TextEdits;
        var razorSourceText = codeDocument.Source.Text;
        var originalSyntaxTree = await documentContext.Snapshot.GetCSharpSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        var newText = codeDocument.GetCSharpSourceText().WithChanges(edits);
        var newSyntaxTree = originalSyntaxTree.WithChangedText(newText);

        AddDirectlyMappedChanges(edits, codeDocument, ref builder.AsRef(), cancellationToken);

        var oldUsings = await AddUsingsHelper.FindUsingDirectiveStringsAsync(
            originalSyntaxTree,
            cancellationToken).ConfigureAwait(false);

        var newUsings = await AddUsingsHelper.FindUsingDirectiveStringsAsync(
            newSyntaxTree,
            cancellationToken).ConfigureAwait(false);

        var addedUsings = Delta.Compute(oldUsings, newUsings);
        var removedUsings = Delta.Compute(newUsings, oldUsings);

        AddRemovedNamespaceEdits(removedUsings, codeDocument, ref builder.AsRef(), cancellationToken);
        AddNewNamespaceEdits(addedUsings, codeDocument, ref builder.AsRef(), cancellationToken);

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

    private static void AddRemovedNamespaceEdits(ImmutableArray<string> removedUsings, RazorCodeDocument codeDocument, ref PooledArrayBuilder<TextChange> builder, CancellationToken cancellationToken)
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

    private static void AddNewNamespaceEdits(ImmutableArray<string> addedUsings, RazorCodeDocument codeDocument, ref PooledArrayBuilder<TextChange> builder, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var edits = AddUsingsHelper.GenerateUsingsEdits(codeDocument, addedUsings);
        if (edits.Length > 0)
        {
            var sourceText = codeDocument.Source.Text;
            var textChanges = edits.Select(e => new TextChange(sourceText.GetTextSpan(e.Range), e.NewText));
            builder.AddRange(textChanges);
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
        // Ensure that the changes are sorted by start position otherwise
        // the normalization logic will not work.
        Debug.Assert(changes.SequenceEqual(changes.OrderBy(static c => c.Span.Start)));

        using var normalizedEdits = new PooledArrayBuilder<TextChange>(changes.Length);
        var remaining = changes.AsSpan();

        var droppedEdits = 0;
        while (remaining is not [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (remaining is [var edit, var nextEdit, ..])
            {
                if (edit.Span == nextEdit.Span)
                {
                    normalizedEdits.Add(nextEdit);
                    remaining = remaining[1..];

                    if (edit.NewText != nextEdit.NewText)
                    {
                        droppedEdits++;
                    }
                }
                else if (edit.Span.Contains(nextEdit.Span))
                {
                    // Cases where there was a removal and addition on the same
                    // line err to taking the addition. This can happen in the
                    // case of a namespace rename
                    if (edit.Span.Start == nextEdit.Span.Start)
                    {
                        if (string.IsNullOrEmpty(edit.NewText) && !string.IsNullOrEmpty(nextEdit.NewText))
                        {
                            // Don't count this as a dropped edit, it is expected
                            // in the case of a rename
                            normalizedEdits.Add(new TextChange(edit.Span, nextEdit.NewText));
                            remaining = remaining[1..];
                        }
                        else
                        {
                            normalizedEdits.Add(edit);
                            remaining = remaining[1..];
                            droppedEdits++;
                        }
                    }
                    else
                    {
                        normalizedEdits.Add(edit);

                        remaining = remaining[1..];
                        droppedEdits++;
                    }
                }
                else if (nextEdit.Span.Contains(edit.Span))
                {
                    // Add the edit that is contained in the other edit
                    // and skip the next edit.
                    normalizedEdits.Add(nextEdit);
                    remaining = remaining[1..];
                    droppedEdits++;
                }
                else
                {
                    normalizedEdits.Add(edit);
                }
            }
            else
            {
                normalizedEdits.Add(remaining[0]);
            }

            remaining = remaining[1..];
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
