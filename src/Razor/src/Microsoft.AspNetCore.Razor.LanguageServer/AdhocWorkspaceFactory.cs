// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal sealed class AdhocWorkspaceFactory(IHostServicesProvider hostServicesProvider) : IAdhocWorkspaceFactory
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
