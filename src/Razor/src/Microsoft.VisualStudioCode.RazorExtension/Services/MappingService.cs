// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.VisualStudioCode.RazorExtension.Services;

internal class MappingService(IRazorClientLanguageServerManager razorClientLanguageServerManager) : IRazorMappingService
{
    private const string RazorMapSpansEndpoint = "razor/mapSpans";
    private const string RazorMapTextChangesEndpoint = "razor/mapTextChanges";

    private readonly IRazorClientLanguageServerManager _razorClientLanguageServerManager = razorClientLanguageServerManager;

    public async Task<ImmutableArray<RazorMappedSpanResult>> MapSpansAsync(Document document, IEnumerable<TextSpan> spans, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(document.FilePath))
        {
            return [];
        }

        var mapParams = new RazorMapSpansParams()
        {
            CSharpDocument = new()
            {
                Uri = new(document.FilePath)
            },
            Spans = spans.Select(span => span.ToRazorTextSpan()).ToArray()
        };

        var response = await _razorClientLanguageServerManager.SendRequestAsync<RazorMapSpansParams, RazorMapSpansResponse?>(
            RazorMapSpansEndpoint,
            mapParams,
            cancellationToken).ConfigureAwait(false);

        if (response is not { Spans.Length: > 0, Ranges.Length: > 0 })
        {
            return [];
        }

        Debug.Assert(response.Spans.Length == spans.Count(), "The number of mapped spans should match the number of input spans.");
        Debug.Assert(response.Ranges.Length == spans.Count(), "The number of mapped ranges should match the number of input spans.");

        var builder = ImmutableArray.CreateBuilder<RazorMappedSpanResult>(response.Spans.Length);
        var filePath = response.RazorDocument.Uri.GetDocumentFilePath();

        for (var i = 0; i < response.Spans.Length; i++)
        {
            var span = response.Spans[i];
            var range = response.Ranges[i];
            builder.Add(new RazorMappedSpanResult(filePath, range.ToLinePositionSpan(), span.ToTextSpan()));
        }

        return builder.ToImmutable();
    }

    public async Task<ImmutableArray<RazorMappedEditResult>> MapTextChangesAsync(Document oldDocument, Document newDocument, CancellationToken cancellationToken)
    {
        var changes = await newDocument.GetTextChangesAsync(oldDocument, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(newDocument.FilePath))
        {
            return [];
        }

        var mapParams = new RazorMapTextChangesParams()
        {
            CSharpDocument = new()
            {
                Uri = new(newDocument.FilePath)
            },
            TextChanges = changes.Select(c => c.ToRazorTextChange()).ToArray()
        };

        var response = await _razorClientLanguageServerManager.SendRequestAsync<RazorMapTextChangesParams, RazorMapTextChangesResponse?>(
            RazorMapTextChangesEndpoint,
            mapParams,
            cancellationToken).ConfigureAwait(false);

        if (response is not { MappedTextChanges.Length: > 0 })
        {
            return [];
        }

        Debug.Assert(response.MappedTextChanges.Length == changes.Count(), "The number of mapped text changes should match the number of input text changes.");
        var filePath = response.RazorDocument.Uri.GetDocumentFilePath();

        var result = new RazorMappedEditResult(filePath, response.MappedTextChanges.Select(mappedChange => mappedChange.ToTextChange()).ToArray());
        return [result];
    }
}
