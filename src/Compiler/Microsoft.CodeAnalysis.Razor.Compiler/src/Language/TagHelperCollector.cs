// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.Language;

public abstract partial class TagHelperCollector<T>(
    Compilation compilation,
    IAssemblySymbol? targetAssembly)
    where T : TagHelperCollector<T>
{
    // This type is generic to ensure that each descendent gets its own instance of this field.
    private static readonly ConditionalWeakTable<IAssemblySymbol, Cache> s_perAssemblyCaches = new();

    private readonly Compilation _compilation = compilation;
    private readonly IAssemblySymbol? _targetAssembly = targetAssembly;

    protected virtual bool IncludeNestedTypes => false;

    protected abstract bool IsCandidateType(INamedTypeSymbol type);

    protected abstract void Collect(
        INamedTypeSymbol type,
        ICollection<TagHelperDescriptor> results,
        CancellationToken cancellationToken);

    public void Collect(TagHelperDescriptorProviderContext context, CancellationToken cancellationToken)
    {
        if (_targetAssembly is not null)
        {
            Collect(_targetAssembly, context.Results, cancellationToken);
        }
        else
        {
            Collect(_compilation.Assembly, context.Results, cancellationToken);

            foreach (var reference in _compilation.References)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                {
                    // Check to see if we already have tag helpers cached for this assembly
                    // and use the cached versions if we do. Roslyn shares PE assembly symbols
                    // across compilations, so this ensures that we don't produce new tag helpers
                    // for the same assemblies over and over again.

                    var assemblySymbolData = SymbolCache.GetAssemblySymbolData(assembly);
                    if (!assemblySymbolData.MightContainTagHelpers)
                    {
                        continue;
                    }

                    var includeDocumentation = context.IncludeDocumentation;
                    var excludeHidden = context.ExcludeHidden;

                    var cache = s_perAssemblyCaches.GetValue(assembly, static assembly => new Cache());
                    if (!cache.TryGet(includeDocumentation, excludeHidden, out var tagHelpers))
                    {
                        using var _ = ListPool<TagHelperDescriptor>.GetPooledObject(out var referenceTagHelpers);
                        Collect(assembly, referenceTagHelpers, cancellationToken);

                        tagHelpers = cache.Add(referenceTagHelpers.ToArrayOrEmpty(), includeDocumentation, excludeHidden);
                    }

                    foreach (var tagHelper in tagHelpers)
                    {
                        context.Results.Add(tagHelper);
                    }
                }
            }
        }
    }

    protected virtual void Collect(
        IAssemblySymbol assembly,
        ICollection<TagHelperDescriptor> results,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var includeNestedTypes = IncludeNestedTypes;

        using var stack = new PooledArrayBuilder<INamespaceOrTypeSymbol>();
        using var temp = new PooledArrayBuilder<INamespaceOrTypeSymbol>();

        stack.Push(assembly.GlobalNamespace);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current = stack.Pop();

            switch (current)
            {
                case INamespaceSymbol namespaceSymbol:
                    // Note: Add the members to temp first and then push them
                    // onto the stack in reverse to ensure that they're
                    // popped off in the correct order.
                    foreach (var member in namespaceSymbol.GetMembers())
                    {
                        temp.Add(member);
                    }

                    for (var i = temp.Count - 1; i >= 0; i--)
                    {
                        stack.Push(temp[i]);
                    }

                    temp.Clear();

                    break;

                case INamedTypeSymbol typeSymbol:

                    if (IsCandidateType(typeSymbol))
                    {
                        // We have a candidate. Collect it.
                        Collect(typeSymbol, results, cancellationToken);
                    }

                    if (includeNestedTypes && typeSymbol.DeclaredAccessibility == Accessibility.Public)
                    {
                        // Note: Add the members to temp first and then push them
                        // onto the stack in reverse to ensure that they're
                        // popped off in the correct order.
                        foreach (var member in typeSymbol.GetTypeMembers())
                        {
                            temp.Add(member);
                        }

                        for (var i = temp.Count - 1; i >= 0; i--)
                        {
                            stack.Push(temp[i]);
                        }

                        temp.Clear();
                    }

                    break;
            }
        }
    }
}
