// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.Language;

internal abstract class TagHelperCollector<T>
    where T : ITagHelperDescriptorProvider
{
    // This type is generic to ensure that each descendent gets its own instance of this field.
    private static readonly ConditionalWeakTable<IAssemblySymbol, TagHelperDescriptor[]> s_perAssemblyTagHelperCache = new();

    private readonly Compilation _compilation;
    private readonly ISymbol? _targetSymbol;

    protected TagHelperCollector(Compilation compilation, ISymbol? targetSymbol)
    {
        _compilation = compilation;
        _targetSymbol = targetSymbol;
    }

    protected abstract void Collect(ISymbol symbol, ICollection<TagHelperDescriptor> results);

    public void Collect(ICollection<TagHelperDescriptor> results)
    {
        if (_targetSymbol is not null)
        {
            Collect(_targetSymbol, results);
        }
        else
        {
            Collect(_compilation.Assembly.GlobalNamespace, results);

            foreach (var reference in _compilation.References)
            {
                if (_compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                {
                    TagHelperDescriptor[] tagHelpers;

                    lock (s_perAssemblyTagHelperCache)
                    {
                        // Check to see if we already have tag helpers cached for this assembly
                        // and use them cached versions if we do. Roslyn shares PE assembly symbols
                        // across compilations, so this ensures that we don't produce new tag helpers
                        // for the same assemblies over and over again.
                        if (!s_perAssemblyTagHelperCache.TryGetValue(assembly, out tagHelpers))
                        {
                            using var _ = ListPool<TagHelperDescriptor>.GetPooledObject(out var referenceTagHelpers);
                            Collect(assembly.GlobalNamespace, referenceTagHelpers);

                            tagHelpers = referenceTagHelpers.ToArrayOrEmpty();

                            s_perAssemblyTagHelperCache.Add(assembly, tagHelpers);
                        }
                    }

                    foreach (var tagHelper in tagHelpers)
                    {
                        results.Add(tagHelper);
                    }
                }
            }
        }
    }
}
