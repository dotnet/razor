// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.Language.Legacy;

namespace Microsoft.AspNetCore.Razor.Language.Extensions;

public static class Utf8HtmlLiteralsDirective
{
    public static readonly DirectiveDescriptor Directive = DirectiveDescriptor.CreateDirective(
        SyntaxConstants.CSharp.Utf8HtmlLiterals,
        DirectiveKind.SingleLine,
        builder =>
        {
            builder.AddTypeToken(Resources.Utf8HtmlLiterals_TypeToken_Name, Resources.Utf8HtmlLiterals_TypeToken_Description);
            builder.Usage = DirectiveUsage.FileScopedSinglyOccurring;
            builder.Description = Resources.Utf8HtmlLiterals_Description;
        });

    public static void Register(RazorProjectEngineBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.AddDirective(Directive, FileKinds.Legacy);
        builder.Features.Add(new Utf8HtmlLiteralsDirectivePass());
    }

    #region Obsolete
    [Obsolete("This method is obsolete and will be removed in a future version.")]
    public static void Register(IRazorEngineBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.AddDirective(Directive);
        builder.Features.Add(new Utf8HtmlLiteralsDirectivePass());
    }
    #endregion
}
