// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using Position = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;
using TextEdit = OmniSharp.Extensions.LanguageServer.Protocol.Models.TextEdit;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal abstract class RazorDocumentMappingService
    {
        public abstract TextEdit[] GetProjectedDocumentEdits(RazorCodeDocument codeDocument, TextEdit[] edits);

        public abstract bool TryMapFromProjectedDocumentRange(RazorCodeDocument codeDocument, Range projectedRange, [NotNullWhen(true)] out Range? originalRange);

        public abstract bool TryMapFromProjectedDocumentRange(RazorCodeDocument codeDocument, Range projectedRange, MappingBehavior mappingBehavior, [NotNullWhen(true)] out Range? originalRange);

        public abstract bool TryMapToProjectedDocumentRange(RazorCodeDocument codeDocument, Range originalRange, [NotNullWhen(true)] out Range? projectedRange);

        public abstract bool TryMapFromProjectedDocumentPosition(RazorCodeDocument codeDocument, int csharpAbsoluteIndex, [NotNullWhen(true)] out Position? originalPosition, out int originalIndex);

        public abstract bool TryMapToProjectedDocumentPosition(RazorCodeDocument codeDocument, int absoluteIndex, [NotNullWhen(true)] out Position? projectedPosition, out int projectedIndex);

        public abstract bool TryMapToProjectedDocumentOrNextCSharpPosition(RazorCodeDocument codeDocument, int absoluteIndex, [NotNullWhen(true)] out Position? projectedPosition, out int projectedIndex);

        public abstract RazorLanguageKind GetLanguageKind(RazorCodeDocument codeDocument, int originalIndex);
    }
}
