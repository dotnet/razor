// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;

namespace Microsoft.AspNetCore.Razor.Language.Components;

internal class ComponentStaticPageDirective
{
    public static readonly DirectiveDescriptor Directive = DirectiveDescriptor.CreateDirective(
        "staticpage",
        DirectiveKind.SingleLine,
        builder =>
        {
            builder.Usage = DirectiveUsage.FileScopedSinglyOccurring;
            builder.Description = ComponentResources.StaticPageDirective_Description;
        });

    public static void Register(RazorProjectEngineBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.AddDirective(Directive, FileKinds.Component, FileKinds.ComponentImport);
        builder.Features.Add(new ComponentStaticPagePass());
    }
}
