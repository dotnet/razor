// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.Language;

public abstract partial class TagHelperCollector<T>
    where T : ITagHelperDescriptorProvider
{
    // This type is generic to ensure that each descendent gets its own instance of this field.
    private static readonly ConditionalWeakTable<IAssemblySymbol, Cache> s_perAssemblyCaches = new();

    private readonly Compilation _compilation;
    private readonly ISymbol? _targetSymbol;

    protected TagHelperCollector(Compilation compilation, ISymbol? targetSymbol)
    {
        _compilation = compilation;
        _targetSymbol = targetSymbol;
    }

    private static bool IsTagHelperAssembly(IAssemblySymbol assembly)
    {
        // This as a simple yet high-value optimization that excludes the vast majority of
        // assemblies that (by definition) can't contain a component.
        return assembly.Name != null && !assembly.Name.StartsWith("System.", StringComparison.Ordinal);
    }

    protected abstract void Collect(ISymbol symbol, ICollection<TagHelperDescriptor> results);

    public void Collect(TagHelperDescriptorProviderContext context)
    {
        if (_targetSymbol is not null)
        {
            Collect(_targetSymbol, context.Results);
        }
        else
        {
            Collect(_compilation.Assembly.GlobalNamespace, context.Results);

            foreach (var reference in _compilation.References)
            {
                if (_compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                {
                    if (!IsTagHelperAssembly(assembly))
                    {
                        continue;
                    }

                    TagHelperDescriptor[]? tagHelpers;

                    lock (s_perAssemblyCaches)
                    {
                        // Check to see if we already have tag helpers cached for this assembly
                        // and use them cached versions if we do. Roslyn shares PE assembly symbols
                        // across compilations, so this ensures that we don't produce new tag helpers
                        // for the same assemblies over and over again.
                        if (!s_perAssemblyCaches.TryGetValue(assembly, out var cache) ||
                            !cache.TryGet(context.IncludeDocumentation, context.ExcludeHidden, out tagHelpers))
                        {
                            using var _ = ListPool<TagHelperDescriptor>.GetPooledObject(out var referenceTagHelpers);
                            Collect(assembly.GlobalNamespace, referenceTagHelpers);

                            tagHelpers = referenceTagHelpers.ToArrayOrEmpty();

                            if (cache is null)
                            {
                                cache = new();
                                s_perAssemblyCaches.Add(assembly, cache);
                            }

                            cache.Add(tagHelpers, context.IncludeDocumentation, context.ExcludeHidden);
                        }
                    }

                    foreach (var tagHelper in tagHelpers)
                    {
                        context.Results.Add(tagHelper);
                    }
                }
            }
        }
    }
}
