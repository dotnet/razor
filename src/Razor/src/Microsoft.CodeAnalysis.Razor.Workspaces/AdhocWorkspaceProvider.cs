// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

internal sealed class AdhocWorkspaceProvider(IHostServicesProvider hostServicesProvider) : IDisposable
{
    private readonly Lazy<AdhocWorkspace> _lazyWorkspace = new(() => CreateWorkspace(hostServicesProvider));

    private static AdhocWorkspace CreateWorkspace(IHostServicesProvider hostServicesProvider)
    {
        var fallbackServices = hostServicesProvider.GetServices();
        var services = AdhocServices.Create(
            workspaceServices: [],
            languageServices: [],
            fallbackServices);

        return new AdhocWorkspace(services);
    }

    public AdhocWorkspace GetOrCreate()
    {
        return _lazyWorkspace.Value;
    }

    public void Dispose()
    {
        if (_lazyWorkspace.IsValueCreated)
        {
            _lazyWorkspace.Value.Dispose();
        }
    }
}
