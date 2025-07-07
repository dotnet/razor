// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

internal static class IntermediateNodeFactory
{
    public static IntermediateToken CSharpToken(string content, SourceSpan? source = null)
        => new(TokenKind.CSharp, content, source);

    public static IntermediateToken CSharpToken(object factoryArgument, Func<object, string> contentFactory, SourceSpan? source = null)
        => new(TokenKind.CSharp, LazyContent.Create(factoryArgument, contentFactory), source);

    public static IntermediateToken HtmlToken(string content, SourceSpan? source = null)
        => new(TokenKind.Html, content, source);

    public static IntermediateToken HtmlToken(object factoryArgument, Func<object, string> contentFactory, SourceSpan? source = null)
        => new(TokenKind.Html, LazyContent.Create(factoryArgument, contentFactory), source);
}
