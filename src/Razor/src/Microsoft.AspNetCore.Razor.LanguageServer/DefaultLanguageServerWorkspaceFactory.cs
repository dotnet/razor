// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultLanguageServerWorkspaceFactory : LanguageServerWorkspaceFactory
    {
        private readonly HostWorkspaceServicesProvider _hostWorkspaceServicesProvider;

        public DefaultLanguageServerWorkspaceFactory(HostWorkspaceServicesProvider hostWorkspaceServicesProvider)
        {
            if (hostWorkspaceServicesProvider is null)
            {
                throw new ArgumentNullException(nameof(hostWorkspaceServicesProvider));
            }

            _hostWorkspaceServicesProvider = hostWorkspaceServicesProvider;
        }

        public override LanguageServerWorkspace Create() => Create(Enumerable.Empty<IWorkspaceService>());

        public override LanguageServerWorkspace Create(IEnumerable<IWorkspaceService> workspaceServices)
        {
            if (workspaceServices is null)
            {
                throw new ArgumentNullException(nameof(workspaceServices));
            }

            var fallbackServices = _hostWorkspaceServicesProvider.GetServices();
            var services = AdhocServices.Create(
                workspaceServices,
                razorLanguageServices: Enumerable.Empty<ILanguageService>(),
                fallbackServices);
            var workspace = new LanguageServerWorkspace(services);
            return workspace;
        }
    }
}
