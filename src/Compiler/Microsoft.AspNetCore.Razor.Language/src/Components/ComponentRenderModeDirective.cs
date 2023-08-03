// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable enable

using System;

namespace Microsoft.AspNetCore.Razor.Language.Components;
internal class ComponentRenderModeDirective
{
    // PROTOTYPE: localization

    public static readonly DirectiveDescriptor Directive = DirectiveDescriptor.CreateDirective(
       "rendermode",
       DirectiveKind.SingleLine,
       builder =>
       {
           // PROTOTYPE: we only support the identifier version right now
           builder.AddNamespaceToken("RenderMode", "The RenderMode to use");
           builder.Usage = DirectiveUsage.FileScopedSinglyOccurring;
           builder.Description = "Set the render mode for this component.";
       });


    public static void Register(RazorProjectEngineBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.AddDirective(Directive, FileKinds.Component);
        builder.Features.Add(new ComponentRenderModeDirectivePass());
    }
}
