// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

internal sealed class LazyIntermediateToken(
    TokenKind kind,
    object factoryArgument,
    Func<object, string> contentFactory,
    SourceSpan? source)
    : IntermediateToken(kind, content: null, source)
{
    private object _factoryArgument = factoryArgument;
    private Func<object, string> _contentFactory = contentFactory;

    public override string? Content
    {
        get
        {
            if (base.Content == null && _contentFactory != null)
            {
                UpdateContent(_contentFactory(_factoryArgument));

                _factoryArgument = null!;
                _contentFactory = null!;
            }

            return base.Content;
        }
    }
}
