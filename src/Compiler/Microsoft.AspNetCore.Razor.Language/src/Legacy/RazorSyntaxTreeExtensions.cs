// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

internal static class RazorSyntaxTreeExtensions
{
    public static ImmutableArray<ClassifiedSpanInternal> GetClassifiedSpans(this RazorSyntaxTree syntaxTree)
    {
        if (syntaxTree == null)
        {
            throw new ArgumentNullException(nameof(syntaxTree));
        }

        using var _ = ArrayBuilderPool<ClassifiedSpanInternal>.GetPooledObject(out var builder);

        var visitor = new ClassifiedSpanVisitor(syntaxTree.Source, builder);
        visitor.Visit(syntaxTree.Root);

        return builder.DrainToImmutable();
    }

    public static ImmutableArray<TagHelperSpanInternal> GetTagHelperSpans(this RazorSyntaxTree syntaxTree)
    {
        if (syntaxTree == null)
        {
            throw new ArgumentNullException(nameof(syntaxTree));
        }

        using var _ = ArrayBuilderPool<TagHelperSpanInternal>.GetPooledObject(out var builder);

        var visitor = new TagHelperSpanVisitor(syntaxTree.Source, builder);
        visitor.Visit(syntaxTree.Root);

        return builder.DrainToImmutable();
    }
}
