// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

internal static class IntermediateNodeFactory
{
    public static IntermediateToken CSharpToken(string content, SourceSpan? source = null)
        => new() { Content = content, Kind = TokenKind.CSharp, Source = source };

    public static LazyIntermediateToken CSharpToken(object factoryArgument, Func<object, string> contentFactory, SourceSpan? source = null)
        => new() { FactoryArgument = factoryArgument, ContentFactory = contentFactory, Kind = TokenKind.CSharp, Source = source };

    public static IntermediateToken HtmlToken(string content, SourceSpan? source = null)
        => new() { Content = content, Kind = TokenKind.Html, Source = source };

    public static LazyIntermediateToken HtmlToken(object factoryArgument, Func<object, string> contentFactory, SourceSpan? source = null)
        => new() { FactoryArgument = factoryArgument, ContentFactory = contentFactory, Kind = TokenKind.Html, Source = source };
}
