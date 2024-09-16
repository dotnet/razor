// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

internal class AdhocWorkspaceFactory(IHostServicesProvider hostServicesProvider) : IAdhocWorkspaceFactory
{
    public AdhocWorkspace Create()
    {
        var fallbackServices = hostServicesProvider.GetServices();
        var services = AdhocServices.Create(
            workspaceServices: [],
            languageServices: [],
            fallbackServices);

        return new AdhocWorkspace(services);
    }
}
