// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

public sealed class HtmlIntermediateToken : IntermediateToken
{
    internal HtmlIntermediateToken(string content, SourceSpan? span = null)
        : base(TokenKind.Html, content, span)
    {
    }

    internal HtmlIntermediateToken(LazyContent lazyContent, SourceSpan? span = null)
        : base(TokenKind.Html, lazyContent, span)
    {
    }
}
