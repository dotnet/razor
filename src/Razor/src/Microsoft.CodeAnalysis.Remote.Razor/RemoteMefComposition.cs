﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal static class RemoteMefComposition
{
    internal static readonly ImmutableArray<Assembly> RemoteHostAssemblies = [typeof(RemoteMefComposition).Assembly];

#pragma warning disable VSTHRD012 // Provide JoinableTaskFactory where allowed
    private static readonly AsyncLazy<ExportProvider> s_exportProviderLazy = new(CreateExportProviderAsync);
#pragma warning restore VSTHRD012 // Provide JoinableTaskFactory where allowed

    public static Task<ExportProvider> GetExportProviderAsync()
        => s_exportProviderLazy.GetValueAsync();

    // Inspired by https://github.com/dotnet/roslyn/blob/25aa74d725e801b8232dbb3e5abcda0fa72da8c5/src/Workspaces/Remote/ServiceHub/Host/RemoteWorkspaceManager.cs#L77
    private static async Task<ExportProvider> CreateExportProviderAsync()
    {
        var resolver = new Resolver(SimpleAssemblyLoader.Instance);
        var discovery = new AttributedPartDiscovery(resolver, isNonPublicSupported: true); // MEFv2 only
        var parts = await discovery.CreatePartsAsync(RemoteHostAssemblies).ConfigureAwait(false);
        var catalog = ComposableCatalog.Create(resolver).AddParts(parts);

        var configuration = CompositionConfiguration.Create(catalog).ThrowOnErrors();
        var runtimeComposition = RuntimeComposition.CreateRuntimeComposition(configuration);
        var exportProviderFactory = runtimeComposition.CreateExportProviderFactory();
        return exportProviderFactory.CreateExportProvider();
    }

    private sealed class SimpleAssemblyLoader : IAssemblyLoader
    {
        public static readonly IAssemblyLoader Instance = new SimpleAssemblyLoader();

        public Assembly LoadAssembly(AssemblyName assemblyName)
            => Assembly.Load(assemblyName);

        public Assembly LoadAssembly(string assemblyFullName, string? codeBasePath)
        {
            var assemblyName = new AssemblyName(assemblyFullName);
            if (!string.IsNullOrEmpty(codeBasePath))
            {
#pragma warning disable SYSLIB0044 // https://github.com/dotnet/roslyn/issues/71510
                assemblyName.CodeBase = codeBasePath;
#pragma warning restore SYSLIB0044
            }

            return LoadAssembly(assemblyName);
        }
    }
}
