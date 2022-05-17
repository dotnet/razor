// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions
{
    internal static class RazorDocumentMappingServiceExtensions
    {
        public static bool TryMapToProjectedDocumentRange(this RazorDocumentMappingService service, RazorCodeDocument codeDocument, Range originalRange, [NotNullWhen(true)] out Range? projectedRange)
        {
            var result = service.TryMapToProjectedDocumentRange(codeDocument, originalRange.AsOmniSharpRange(), out var projectedOmniSharpRange);

            projectedRange = projectedOmniSharpRange?.AsVSRange();

            return result;
        }

        public static bool TryMapFromProjectedDocumentRange(this RazorDocumentMappingService service, RazorCodeDocument codeDocument, Range originalRange, [NotNullWhen(true)] out Range? projectedRange)
        {
            var result = service.TryMapFromProjectedDocumentRange(codeDocument, originalRange.AsOmniSharpRange(), out var projectedOmniSharpRange);

            projectedRange = projectedOmniSharpRange?.AsVSRange();

            return result;
        }
    }
}
