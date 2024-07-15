// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal static class RemoteMefComposition
{
    internal static readonly ImmutableArray<Assembly> RemoteHostAssemblies = [typeof(RemoteMefComposition).Assembly];

    private static readonly AsyncLazy<CompositionConfiguration> s_lazyConfiguration = new(CreateConfigurationAsync, joinableTaskFactory: null);

    private static readonly AsyncLazy<ExportProvider> s_lazyExportProvider = new(CreateExportProviderAsync, joinableTaskFactory: null);

    public static Task<CompositionConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default)
        => s_lazyConfiguration.GetValueAsync(cancellationToken);

    public static Task<ExportProvider> GetExportProviderAsync(CancellationToken cancellationToken = default)
        => s_lazyExportProvider.GetValueAsync(cancellationToken);

    private static async Task<CompositionConfiguration> CreateConfigurationAsync()
    {
        var resolver = new Resolver(SimpleAssemblyLoader.Instance);
        var discovery = new AttributedPartDiscovery(resolver, isNonPublicSupported: true); // MEFv2 only
        var parts = await discovery.CreatePartsAsync(RemoteHostAssemblies).ConfigureAwait(false);
        var catalog = ComposableCatalog.Create(resolver).AddParts(parts);

        return CompositionConfiguration.Create(catalog).ThrowOnErrors();
    }

    // Internal for testing
    // Inspired by https://github.com/dotnet/roslyn/blob/25aa74d725e801b8232dbb3e5abcda0fa72da8c5/src/Workspaces/Remote/ServiceHub/Host/RemoteWorkspaceManager.cs#L77
    private static async Task<ExportProvider> CreateExportProviderAsync()
    {
        var configuration = await s_lazyConfiguration.GetValueAsync();
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
