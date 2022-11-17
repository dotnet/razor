﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting;

internal enum FormattingBlockKind
{
    // Code
    Statement,
    Directive,
    Expression,

    // Markup
    Markup,
    Template,

    // Special
    Comment,
    Tag,
    HtmlComment
}
