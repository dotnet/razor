// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Common
{
    public class AdhocServices : HostServices
    {
        private readonly IEnumerable<IWorkspaceService> _workspaceServices;
        private readonly IEnumerable<ILanguageService> _razorLanguageServices;
        private readonly HostWorkspaceServices _fallbackServices;

        private AdhocServices(
            IEnumerable<IWorkspaceService> workspaceServices,
            IEnumerable<ILanguageService> razorLanguageServices,
            HostWorkspaceServices fallbackServices)
        {
            if (workspaceServices == null)
            {
                throw new ArgumentNullException(nameof(workspaceServices));
            }

            if (razorLanguageServices == null)
            {
                throw new ArgumentNullException(nameof(razorLanguageServices));
            }

            if (fallbackServices is null)
            {
                throw new ArgumentNullException(nameof(fallbackServices));
            }

            _workspaceServices = workspaceServices;
            _razorLanguageServices = razorLanguageServices;
            _fallbackServices = fallbackServices;
        }

        protected override HostWorkspaceServices CreateWorkspaceServices(Workspace workspace)
        {
            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            return new AdhocWorkspaceServices(this, _workspaceServices, _razorLanguageServices, workspace, _fallbackServices);
        }

        public static HostServices Create(IEnumerable<IWorkspaceService> workspaceServices, IEnumerable<ILanguageService> razorLanguageServices, HostWorkspaceServices fallbackServices)
            => new AdhocServices(workspaceServices, razorLanguageServices, fallbackServices);
    }
}
