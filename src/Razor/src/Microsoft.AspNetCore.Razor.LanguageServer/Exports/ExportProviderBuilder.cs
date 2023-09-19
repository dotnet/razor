// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#if !NET472

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Composition;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Exports;

internal sealed class ExportProviderBuilder
{
    public static async Task<ExportProvider> CreateExportProviderAsync(string? extensionPath)
    {
        var baseDirectory = AppContext.BaseDirectory;
        var resolver = new Resolver(new CustomExportAssemblyLoader(baseDirectory));

        var discovery = PartDiscovery.Combine(
            resolver,
            new AttributedPartDiscovery(resolver, isNonPublicSupported: true), // "NuGet MEF" attributes (Microsoft.Composition)
            new AttributedPartDiscoveryV1(resolver));

        var assemblies = new List<Assembly>()
        {
            typeof(ExportProviderBuilder).Assembly
        };

        if (extensionPath is not null && AssemblyLoadContextWrapper.TryLoadExtension(extensionPath, out var extensionAssembly))
        {
            assemblies.Add(extensionAssembly);
        }

        // TODO - we should likely cache the catalog so we don't have to rebuild it every time.
        var catalog = ComposableCatalog.Create(resolver)
            .AddParts(await discovery.CreatePartsAsync(assemblies).ConfigureAwait(true))
            .WithCompositionService(); // Makes an ICompositionService export available to MEF parts to import

        // Assemble the parts into a valid graph.
        var config = CompositionConfiguration.Create(catalog);

        // Verify we have no errors.
        config.ThrowOnErrors();

        // Prepare an ExportProvider factory based on this graph.
        var exportProviderFactory = config.CreateExportProviderFactory();

        // Create an export provider, which represents a unique container of values.
        // You can create as many of these as you want, but typically an app needs just one.
        var exportProvider = exportProviderFactory.CreateExportProvider();

        return exportProvider;
    }
}

#endif
