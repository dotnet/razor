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

        public static bool TryMapFromProjectedDocumentVSRange(this RazorDocumentMappingService service, RazorCodeDocument codeDocument, Range range, MappingBehavior mappingBehavior, [NotNullWhen(true)] out Range? originalRange)
        {
            if (service.TryMapFromProjectedDocumentRange(codeDocument, range.AsOmniSharpRange(), mappingBehavior, out var omniOriginalRange))
            {
                originalRange = omniOriginalRange.AsVSRange();
                return true;
            }

            originalRange = null;
            return false;
        }

        public static bool TryMapToProjectedDocumentVSPosition(this RazorDocumentMappingService service, RazorCodeDocument codeDocument, int absoluteIndex, [NotNullWhen(true)] out Position? projectedPosition, out int projectedIndex)
        {
            if (service.TryMapToProjectedDocumentPosition(codeDocument, absoluteIndex, out var omniProjectedPosition, out projectedIndex))
            {
                projectedPosition = omniProjectedPosition.AsVSPosition();
                return true;
            }

            projectedPosition = null;
            return false;
        }

        public static bool TryMapToProjectedDocumentVSRange(this RazorDocumentMappingService service, RazorCodeDocument razorCodeDocument, Range range, [NotNullWhen(true)] out Range? projectedRange)
        {
            if (service.TryMapToProjectedDocumentRange(razorCodeDocument, range.AsOmniSharpRange(), out var omniRange))
            {
                projectedRange = omniRange.AsVSRange();
                return true;
            }

            projectedRange = null;
            return false;
        }

        public static bool TryMapFromProjectedDocumentVSPosition(this RazorDocumentMappingService service, RazorCodeDocument codeDocument, int csharpAbsoluteIndex, [NotNullWhen(true)] out Position? originalPosition, out int originalIndex)
        {
            var result = service.TryMapFromProjectedDocumentPosition(codeDocument, csharpAbsoluteIndex, out var omniOriginalPosition, out originalIndex);

            originalPosition = omniOriginalPosition?.AsVSPosition();

            return result;
        }
    }
}
