// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

internal static class NodeFactory
{
    public static IntermediateToken CSharpToken(string content, SourceSpan? span = null)
        => new CSharpIntermediateToken(content, span);

    public static LazyIntermediateToken LazyCSharpToken(
        object factoryArgument, Func<object, string> contentFactory, SourceSpan? span = null)
        => new LazyCSharpIntermediateToken(factoryArgument, contentFactory, span);

    public static IntermediateToken HtmlToken(string content, SourceSpan? span = null)
        => new HtmlIntermediateToken(content, span);

    public static LazyIntermediateToken LazyHtmlToken(
        object factoryArgument, Func<object, string> contentFactory, SourceSpan? span = null)
        => new LazyHtmlIntermediateToken(factoryArgument, contentFactory, span);
}
