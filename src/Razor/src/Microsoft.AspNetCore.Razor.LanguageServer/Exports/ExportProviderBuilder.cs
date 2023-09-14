// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#if !NET472

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Composition;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Exports;

internal sealed class ExportProviderBuilder
{
    public static async Task<ExportProvider> CreateExportProviderAsync(
        string? extensionAssemblyPath,
        string? sharedDependenciesPath)
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

        if (extensionAssemblyPath is not null && AssemblyLoadContextWrapper.TryLoadExtension(extensionAssemblyPath, sharedDependenciesPath, out var extensionAssembly))
        {
            assemblies.Add(extensionAssembly);
        }

        // TODO - we should likely cache the catalog so we don't have to rebuild it every time.
        var catalog = ComposableCatalog.Create(resolver)
            .AddParts(await discovery.CreatePartsAsync(assemblies).ConfigureAwait(true))
            .WithCompositionService(); // Makes an ICompositionService export available to MEF parts to import

        // Assemble the parts into a valid graph.
        var config = CompositionConfiguration.Create(catalog);

        // Verify we only have expected errors.
        ThrowOnUnexpectedErrors(config);

        // Prepare an ExportProvider factory based on this graph.
        var exportProviderFactory = config.CreateExportProviderFactory();

        // Create an export provider, which represents a unique container of values.
        // You can create as many of these as you want, but typically an app needs just one.
        var exportProvider = exportProviderFactory.CreateExportProvider();

        return exportProvider;
    }

    private static void ThrowOnUnexpectedErrors(CompositionConfiguration configuration)
    {
        // Verify that we have exactly the MEF errors that we expect.  If we have less or more this needs to be updated to assert the expected behavior.
        var erroredParts = configuration.CompositionErrors.FirstOrDefault()?.SelectMany(error => error.Parts).Select(part => part.Definition.Type.Name) ?? Enumerable.Empty<string>();
        var expectedErroredParts = Array.Empty<string>();
        if (erroredParts.Count() != expectedErroredParts.Length || !erroredParts.All(part => expectedErroredParts.Contains(part)))
        {
            configuration.ThrowOnErrors();
        }
    }
}

#endif
