// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

public sealed class CSharpIntermediateToken : IntermediateToken
{
    internal CSharpIntermediateToken(string content, SourceSpan? span = null)
        : base(TokenKind.CSharp, content, span)
    {
    }

    internal CSharpIntermediateToken(LazyContent lazyContent, SourceSpan? span = null)
        : base(TokenKind.CSharp, lazyContent, span)
    {
    }
}
