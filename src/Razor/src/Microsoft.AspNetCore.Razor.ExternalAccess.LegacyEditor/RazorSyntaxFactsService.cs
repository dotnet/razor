// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.ComponentModel.Composition;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.LegacyEditor;

[System.Composition.Shared]
[Export(typeof(RazorSyntaxFactsService))]
internal class RazorSyntaxFactsService : ILanguageService
{
    public ImmutableArray<ClassifiedSpan> GetClassifiedSpans(RazorCodeDocument document)
    {
        var result = document.GetSyntaxTree().GetClassifiedSpans();

        using var builder = new PooledArrayBuilder<ClassifiedSpan>(capacity: result.Length);

        foreach (var item in result)
        {
            builder.Add(new ClassifiedSpan(
                ConvertSpan(item.Span),
                ConvertSpan(item.BlockSpan),
                (SpanKind)item.SpanKind,
                (BlockKind)item.BlockKind,
                (AcceptedCharacters)item.AcceptedCharacters));
        }

        return builder.DrainToImmutable();
    }

    public ImmutableArray<TagHelperSpan> GetTagHelperSpans(RazorCodeDocument document)
    {
        var result = document.GetSyntaxTree().GetTagHelperSpans();

        using var builder = new PooledArrayBuilder<TagHelperSpan>(capacity: result.Length);

        foreach (var item in result)
        {
            builder.Add(new TagHelperSpan(
                ConvertSpan(item.Span),
                item.Binding));
        }

        return builder.DrainToImmutable();
    }

    private static RazorSourceSpan ConvertSpan(SourceSpan span)
    {
        return new(
            FilePath: span.FilePath,
            AbsoluteIndex: span.AbsoluteIndex,
            LineIndex: span.LineIndex,
            CharacterIndex: span.CharacterIndex,
            Length: span.Length,
            LineCount: span.LineCount,
            EndCharacterIndex: span.EndCharacterIndex);
    }
}
