// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

internal static class NodeFactory
{
    public static CSharpIntermediateToken CSharpToken(string content, SourceSpan? span = null)
        => new(content, span);

    public static CSharpIntermediateToken LazyCSharpToken<T>(
        Func<T, string> contentCreator, T arg, SourceSpan? span = null)
        => new(LazyContent.Create(contentCreator, arg), span);

    public static HtmlIntermediateToken HtmlToken(string content, SourceSpan? span = null)
        => new(content, span);

    public static HtmlIntermediateToken LazyHtmlToken<T>(
        Func<T, string> contentCreater, T arg, SourceSpan? span = null)
        => new(LazyContent.Create(contentCreater, arg), span);
}
