﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language.Syntax;

internal partial class MarkupEndTagSyntax
{
    public bool IsMarkupTransition
        => ((InternalSyntax.MarkupEndTagSyntax)Green).IsMarkupTransition;

    public string GetTagNameWithOptionalBang()
    {
        return Name.IsMissing ? string.Empty : Bang?.Content + Name.Content;
    }
}
