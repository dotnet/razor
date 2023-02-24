// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor;

public static class TagHelperDescriptorProviderContextExtensions
{
    public static Compilation GetCompilation(this TagHelperDescriptorProviderContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return (Compilation)context.Items[typeof(Compilation)];
    }

    [Obsolete($"Use {nameof(SetTypeProvider)} instead.")]
    public static void SetCompilation(this TagHelperDescriptorProviderContext context, Compilation compilation)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.SetTypeProvider(new WellKnownTypeProvider(compilation));
    }

    public static WellKnownTypeProvider GetTypeProvider(this TagHelperDescriptorProviderContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return (WellKnownTypeProvider)context.Items[typeof(WellKnownTypeProvider)];
    }

    public static void SetTypeProvider(this TagHelperDescriptorProviderContext context, WellKnownTypeProvider typeProvider)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.Items[typeof(Compilation)] = typeProvider.Compilation;
        context.Items[typeof(WellKnownTypeProvider)] = typeProvider;
    }
}
