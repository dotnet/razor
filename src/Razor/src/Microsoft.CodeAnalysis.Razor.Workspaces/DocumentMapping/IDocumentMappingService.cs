// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.DocumentMapping;

internal interface IDocumentMappingService
{
    IEnumerable<TextChange> GetHostDocumentEdits(RazorCSharpDocument csharpDocument, ImmutableArray<TextChange> generatedDocumentEdits);

    bool TryMapToHostDocumentRange(RazorCSharpDocument csharpDocument, LinePositionSpan generatedDocumentRange, MappingBehavior mappingBehavior, out LinePositionSpan hostDocumentRange);

    bool TryMapToGeneratedDocumentRange(RazorCSharpDocument csharpDocument, LinePositionSpan hostDocumentRange, out LinePositionSpan generatedDocumentRange);

    bool TryMapToHostDocumentPosition(RazorCSharpDocument csharpDocument, int generatedDocumentIndex, out LinePosition hostDocumentPosition, out int hostDocumentIndex);

    bool TryMapToGeneratedDocumentPosition(RazorCSharpDocument csharpDocument, int hostDocumentIndex, out LinePosition generatedPosition, out int generatedIndex);

    bool TryMapToGeneratedDocumentOrNextCSharpPosition(RazorCSharpDocument csharpDocument, int hostDocumentIndex, out LinePosition generatedPosition, out int generatedIndex);
}
