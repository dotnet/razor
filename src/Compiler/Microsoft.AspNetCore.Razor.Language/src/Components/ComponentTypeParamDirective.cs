﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;

namespace Microsoft.AspNetCore.Razor.Language.Components;

internal static class ComponentTypeParamDirective
{
    public static DirectiveDescriptor Directive = DirectiveDescriptor.CreateDirective(
        "typeparam",
        DirectiveKind.SingleLine,
        builder =>
        {
            builder.AddMemberToken(ComponentResources.TypeParamDirective_Token_Name, ComponentResources.TypeParamDirective_Token_Description);
            builder.Usage = DirectiveUsage.FileScopedMultipleOccurring;
            builder.Description = ComponentResources.TypeParamDirective_Description;
        });

    public static RazorProjectEngineBuilder Register(RazorProjectEngineBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.AddDirective(Directive, FileKinds.Component, FileKinds.ComponentImport);
        return builder;
    }
}
