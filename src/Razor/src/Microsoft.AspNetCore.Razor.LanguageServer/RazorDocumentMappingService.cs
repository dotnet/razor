// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Omni = OmniSharp.Extensions.LanguageServer.Protocol.Models;
using VS = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal abstract class RazorDocumentMappingService
    {
        public abstract VS.TextEdit[] GetProjectedDocumentEdits(RazorCodeDocument codeDocument, VS.TextEdit[] edits);

        public abstract bool TryMapFromProjectedDocumentRange(RazorCodeDocument codeDocument, Omni.Range projectedRange, [NotNullWhen(true)] out Omni.Range? originalRange);

        public abstract bool TryMapFromProjectedDocumentRange(RazorCodeDocument codeDocument, Omni.Range projectedRange, MappingBehavior mappingBehavior, [NotNullWhen(true)] out Omni.Range? originalRange);

        public abstract bool TryMapFromProjectedDocumentVSRange(RazorCodeDocument codeDocument, VS.Range range, MappingBehavior mappingBehavior, [NotNullWhen(true)] out VS.Range? originalRange);

        public abstract bool TryMapToProjectedDocumentRange(RazorCodeDocument codeDocument, Omni.Range originalRange, [NotNullWhen(true)] out Omni.Range? projectedRange);

        public abstract bool TryMapToProjectedDocumentVSRange(RazorCodeDocument razorCodeDocument, Range range, [NotNullWhen(true)] out Range? projectedRange);

        public abstract bool TryMapFromProjectedDocumentVSPosition(RazorCodeDocument codeDocument, int csharpAbsoluteIndex, [NotNullWhen(true)] out VS.Position? originalPosition, out int originalIndex);

        public abstract bool TryMapFromProjectedDocumentPosition(RazorCodeDocument codeDocument, int csharpAbsoluteIndex, [NotNullWhen(true)] out Omni.Position? originalPosition, out int originalIndex);

        public abstract bool TryMapToProjectedDocumentPosition(RazorCodeDocument codeDocument, int absoluteIndex, [NotNullWhen(true)] out Omni.Position? projectedPosition, out int projectedIndex);

        public abstract bool TryMapToProjectedDocumentVSPosition(RazorCodeDocument codeDocument, int absoluteIndex, [NotNullWhen(true)] out VS.Position? projectedPosition, out int projectedIndex);

        public abstract bool TryMapToProjectedDocumentOrNextCSharpPosition(RazorCodeDocument codeDocument, int absoluteIndex, [NotNullWhen(true)] out Omni.Position? projectedPosition, out int projectedIndex);

        public abstract RazorLanguageKind GetLanguageKind(RazorCodeDocument codeDocument, int originalIndex, bool rightAssociative);
    }
}
