// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

internal abstract class LazyIntermediateToken(
    TokenKind kind,
    object factoryArgumnet,
    Func<object, string> contentFactory, SourceSpan? span)
    : IntermediateToken(kind, content: null, span)
{
    private object _factoryArgument = factoryArgumnet;
    private Func<object, string> _contentFactory = contentFactory;

    public override string? Content
    {
        get
        {
            if (base.Content == null && _contentFactory != null)
            {
                Content = _contentFactory(_factoryArgument);

                _factoryArgument = null!;
                _contentFactory = null!;
            }

            return base.Content;
        }
    }
}
