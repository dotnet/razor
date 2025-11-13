// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.Language;

internal sealed class TagHelperDiscoveryService : RazorEngineFeatureBase
{
    private ImmutableArray<ITagHelperDescriptorProvider> _providers;

    protected override void OnInitialized()
    {
        _providers = Engine.GetFeatures<ITagHelperDescriptorProvider>().OrderByAsArray(static x => x.Order);
    }

    public TagHelperDiscoveryResult GetTagHelpers(
        Compilation compilation,
        TagHelperDiscoveryOptions options,
        CancellationToken cancellationToken)
        => GetTagHelpersForCompilation(compilation, options, cancellationToken);

    public TagHelperDiscoveryResult GetTagHelpers(
        Compilation compilation,
        CancellationToken cancellationToken)
        => GetTagHelpersForCompilation(compilation, options: default, cancellationToken);

    public TagHelperDiscoveryResult GetTagHelpers(
        Compilation compilation,
        IAssemblySymbol targetAssembly,
        CancellationToken cancellationToken)
        => GetTagHelpersForAssembly(compilation, targetAssembly, options: default, cancellationToken);

    public TagHelperDiscoveryResult GetTagHelpers(
        Compilation compilation,
        IAssemblySymbol targetAssembly,
        TagHelperDiscoveryOptions options,
        CancellationToken cancellationToken)
        => GetTagHelpersForAssembly(compilation, targetAssembly, options: default, cancellationToken);

    private TagHelperDiscoveryResult GetTagHelpersForCompilation(
        Compilation compilation,
        TagHelperDiscoveryOptions options,
        CancellationToken cancellationToken)
    {
        ArgHelper.ThrowIfNull(compilation);

        if (_providers.IsDefaultOrEmpty)
        {
            return TagHelperDiscoveryResult.Empty;
        }

        var excludeHidden = options.IsFlagSet(TagHelperDiscoveryOptions.ExcludeHidden);
        var includeDocumentation = options.IsFlagSet(TagHelperDiscoveryOptions.IncludeDocumentation);

        // Note: We only collect timings when performing tag helper discovery for an entire compilation.
        // The source generator always performs tag helper discovery per-assembly. However, in non-cohosted
        // scenarios, tooling performs tag helper discovery across the whole compilation and reports telemetry
        // for per-provider timings.

        using var builder = new TagHelperCollection.Builder();
        using var _ = StopwatchPool.GetPooledObject(out var watch);

        var timings = new (string, TimeSpan)[_providers.Length];
        var timingsSpan = timings.AsSpan();

        var context = new TagHelperDescriptorProviderContext(compilation, builder)
        {
            ExcludeHidden = excludeHidden,
            IncludeDocumentation = includeDocumentation
        };

        foreach (var provider in _providers)
        {
            watch.Restart();
            provider.Execute(context, cancellationToken);
            watch.Stop();

            timingsSpan[0] = (provider.GetType().Name, watch.Elapsed);
            timingsSpan = timingsSpan[1..];
        }

        Debug.Assert(timingsSpan.IsEmpty);

        return new(
            builder.ToCollection(),
            ImmutableCollectionsMarshal.AsImmutableArray(timings));
    }

    private TagHelperDiscoveryResult GetTagHelpersForAssembly(
        Compilation compilation,
        IAssemblySymbol targetAssembly,
        TagHelperDiscoveryOptions options,
        CancellationToken cancellationToken)
    {
        ArgHelper.ThrowIfNull(compilation);
        ArgHelper.ThrowIfNull(targetAssembly);

        if (_providers.IsDefaultOrEmpty)
        {
            return TagHelperDiscoveryResult.Empty;
        }

        var excludeHidden = options.IsFlagSet(TagHelperDiscoveryOptions.ExcludeHidden);
        var includeDocumentation = options.IsFlagSet(TagHelperDiscoveryOptions.IncludeDocumentation);

        // Check to see if we already have tag helpers cached for this assembly
        // and use the cached versions if we do. Roslyn shares PE assembly symbols
        // across compilations, so this ensures that we don't produce new tag helpers
        // for the same assemblies over and over again.

        var assemblySymbolData = SymbolCache.GetAssemblySymbolData(targetAssembly);
        if (!assemblySymbolData.MightContainTagHelpers)
        {
            return TagHelperDiscoveryResult.Empty;
        }

        if (assemblySymbolData.TryGetTagHelpers(includeDocumentation, excludeHidden, out var tagHelpers))
        {
            return new(tagHelpers, timings: []);
        }

        // We don't have tag helpers cached for this assembly, so we have to discover them.
        using var builder = new TagHelperCollection.Builder();

        var context = new TagHelperDescriptorProviderContext(compilation, targetAssembly, builder)
        {
            ExcludeHidden = excludeHidden,
            IncludeDocumentation = includeDocumentation
        };

        foreach (var provider in _providers)
        {
            provider.Execute(context, cancellationToken);

            // After each provider run, check the cache to see if another discovery request
            // for the same assembly finished and cached the result. If so, there's no reason to keep going.
            if (assemblySymbolData.TryGetTagHelpers(includeDocumentation, excludeHidden, out tagHelpers))
            {
                return new(tagHelpers, timings: []);
            }
        }

        var result = assemblySymbolData.AddTagHelpers(builder.ToCollection(), includeDocumentation, excludeHidden);

        return new(result, timings: []);
    }
}
