// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Test
{
    internal class TestRazorLSPSpanMappingService : IRazorSpanMappingService
    {
        private readonly LSPDocumentMappingProvider _mappingProvider;
        private readonly Uri _razorUri;
        private readonly SourceText _razorSourceText;
        private readonly SourceText _csharpSourceText;

        public TestRazorLSPSpanMappingService(LSPDocumentMappingProvider mappingProvider, Uri razorUri, SourceText razorSourceText, SourceText csharpSourceText)
        {
            _mappingProvider = mappingProvider;
            _razorUri = razorUri;
            _razorSourceText = razorSourceText;
            _csharpSourceText = csharpSourceText;
        }

        public async Task<ImmutableArray<RazorMappedSpanResult>> MapSpansAsync(Document document, IEnumerable<TextSpan> spans, CancellationToken cancellationToken)
        {
            var projectedRanges = spans.Select(span => span.AsRange(_csharpSourceText)).ToArray();
            var mappedResult = await _mappingProvider.MapToDocumentRangesAsync(
                RazorLanguageKind.CSharp, _razorUri, projectedRanges, CancellationToken.None).ConfigureAwait(false);

            var mappedSpanResults = RazorLSPSpanMappingService.GetMappedSpanResults(_razorUri.LocalPath, _razorSourceText, mappedResult);
            return mappedSpanResults;
        }
    }
}
