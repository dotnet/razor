// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.Razor.DynamicFiles;

internal class RazorMappingService(IDocumentSnapshot document, ITelemetryReporter telemetryReporter, ILoggerFactory loggerFactory) : IRazorMappingService
{
    private readonly IDocumentSnapshot _document = document;
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter;
    private readonly IDocumentMappingService _documentMappingService = new DocumentMappingService(loggerFactory);

    public async Task<ImmutableArray<RazorMappedSpanResult>> MapSpansAsync(Document document, IEnumerable<TextSpan> spans, CancellationToken cancellationToken)
    {
        // Called on an uninitialized document.
        if (_document is null)
        {
            return ImmutableArray<RazorMappedSpanResult>.Empty;
        }

        var output = await _document.GetGeneratedOutputAsync(cancellationToken).ConfigureAwait(false);
        var source = output.Source.Text;

        var csharpDocument = output.GetCSharpDocument();
        var filePath = output.Source.FilePath.AssumeNotNull();

        using var results = new PooledArrayBuilder<RazorMappedSpanResult>();

        foreach (var span in spans)
        {
            if (TryGetMappedSpans(span, source, csharpDocument, out var linePositionSpan, out var mappedSpan))
            {
                results.Add(new(filePath, linePositionSpan, mappedSpan));
            }
            else
            {
                results.Add(default);
            }
        }

        return results.DrainToImmutable();
    }

    public async Task<ImmutableArray<RazorMappedEditoResult>> MapTextChangesAsync(Document oldDocument, Document newDocument, CancellationToken cancellationToken)
    {
        if (_document.FilePath is null)
        {
            return ImmutableArray<RazorMappedEditoResult>.Empty;
        }

        var changes = await newDocument.GetTextChangesAsync(oldDocument, cancellationToken).ConfigureAwait(false);
        var csharpSource = await oldDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var edits = changes.SelectAsArray(c => csharpSource.GetTextEdit(c));
        var results = await RazorEditHelper.MapCSharpEditsAsync(
            edits,
            _document,
            _documentMappingService,
            _telemetryReporter,
            cancellationToken);

        var razorCodeDocument = await _document.GetGeneratedOutputAsync(cancellationToken).ConfigureAwait(false);
        var razorSource = razorCodeDocument.Source.Text;
        var textChanges = results.SelectAsArray(te => razorSource.GetTextChange(te));

        return [new RazorMappedEditoResult() { FilePath = _document.FilePath, TextChanges = textChanges.ToArray() }];
    }

    // Internal for testing.
    internal static bool TryGetMappedSpans(TextSpan span, SourceText source, RazorCSharpDocument output, out LinePositionSpan linePositionSpan, out TextSpan mappedSpan)
    {
        foreach (var mapping in output.SourceMappings)
        {
            var original = mapping.OriginalSpan.AsTextSpan();
            var generated = mapping.GeneratedSpan.AsTextSpan();

            if (!generated.Contains(span))
            {
                // If the search span isn't contained within the generated span, it is not a match.
                // A C# identifier won't cover multiple generated spans.
                continue;
            }

            var leftOffset = span.Start - generated.Start;
            var rightOffset = span.End - generated.End;
            if (leftOffset >= 0 && rightOffset <= 0)
            {
                // This span mapping contains the span.
                mappedSpan = new TextSpan(original.Start + leftOffset, (original.End + rightOffset) - (original.Start + leftOffset));
                linePositionSpan = source.GetLinePositionSpan(mappedSpan);
                return true;
            }
        }

        mappedSpan = default;
        linePositionSpan = default;
        return false;
    }

    private class DocumentMappingService(ILoggerFactory loggerFactory) : AbstractDocumentMappingService(loggerFactory.GetOrCreateLogger<DocumentMappingService>())
    {
    }
}
