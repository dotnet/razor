// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

internal sealed class LazyCSharpIntermediateToken(
    object factoryArgument, Func<object, string> contentFactory, SourceSpan? span = null)
    : LazyIntermediateToken(TokenKind.CSharp, factoryArgument, contentFactory, span)
{
}
