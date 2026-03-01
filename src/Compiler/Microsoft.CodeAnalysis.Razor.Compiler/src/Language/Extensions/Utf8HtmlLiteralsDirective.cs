// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Razor.Language.Legacy;

namespace Microsoft.AspNetCore.Razor.Language.Extensions;

public static class Utf8HtmlLiteralsDirective
{
    public static readonly DirectiveDescriptor Directive = DirectiveDescriptor.CreateDirective(
        SyntaxConstants.CSharp.Utf8HtmlLiteralsKeyword,
        DirectiveKind.SingleLine,
        builder =>
        {
            builder.AddBooleanToken(Resources.Utf8HtmlLiteralsDirective_BooleanToken_Name, Resources.Utf8HtmlLiteralsDirective_BooleanToken_Description);
            builder.Usage = DirectiveUsage.FileScopedSinglyOccurring;
            builder.Description = Resources.Utf8HtmlLiteralsDirective_Description;
        });

    public static void Register(RazorProjectEngineBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.AddDirective(Directive, RazorFileKind.Legacy);
        builder.Features.Add(new Utf8HtmlLiteralsDirectivePass());
    }
}
