// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor;

public sealed class DefaultTagHelperDescriptorProvider : RazorEngineFeatureBase, ITagHelperDescriptorProvider
{
    public int Order { get; set; }

    public void Execute(TagHelperDescriptorProviderContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var typeProvider = context.GetTypeProvider();
        if (typeProvider == null)
        {
            // No compilation, nothing to do.
            return;
        }

        if (!typeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreRazorTagHelpersITagHelper, out var iTagHelper)
            || iTagHelper.TypeKind == TypeKind.Error)
        {
            // Could not find attributes we care about in the compilation. Nothing to do.
            return;
        }

        var types = new List<INamedTypeSymbol>();
        var visitor = new TagHelperTypeVisitor(iTagHelper, types);

        var targetSymbol = context.Items.GetTargetSymbol();
        if (targetSymbol is not null)
        {
            visitor.Visit(targetSymbol);
        }
        else
        {
            var compilation = typeProvider.Compilation;
            visitor.Visit(compilation.Assembly.GlobalNamespace);
            foreach (var reference in compilation.References)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                {
                    if (IsTagHelperAssembly(assembly))
                    {
                        visitor.Visit(assembly.GlobalNamespace);
                    }
                }
            }
        }


        var factory = new DefaultTagHelperDescriptorFactory(typeProvider, context.IncludeDocumentation, context.ExcludeHidden);
        for (var i = 0; i < types.Count; i++)
        {
            var descriptor = factory.CreateDescriptor(types[i]);

            if (descriptor != null)
            {
                context.Results.Add(descriptor);
            }
        }
    }

    private bool IsTagHelperAssembly(IAssemblySymbol assembly)
    {
        return assembly.Name != null && !assembly.Name.StartsWith("System.", StringComparison.Ordinal);
    }
}
