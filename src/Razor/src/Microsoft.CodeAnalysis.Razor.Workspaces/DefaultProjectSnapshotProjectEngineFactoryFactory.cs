﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

[ExportWorkspaceServiceFactory(typeof(ProjectSnapshotProjectEngineFactory))]
internal class DefaultProjectSnapshotProjectEngineFactoryFactory : IWorkspaceServiceFactory
{
    private readonly IFallbackProjectEngineFactory _fallback;
    private readonly Lazy<IProjectEngineFactory, ICustomProjectEngineFactoryMetadata>[] _factories;

    [ImportingConstructor]
    public DefaultProjectSnapshotProjectEngineFactoryFactory(
        IFallbackProjectEngineFactory fallback,
        [ImportMany] Lazy<IProjectEngineFactory, ICustomProjectEngineFactoryMetadata>[] factories)
    {
        if (fallback is null)
        {
            throw new ArgumentNullException(nameof(fallback));
        }

        if (factories is null)
        {
            throw new ArgumentNullException(nameof(factories));
        }

        _fallback = fallback;
        _factories = factories;
    }

    public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices)
    {
        if (workspaceServices is null)
        {
            throw new ArgumentNullException(nameof(workspaceServices));
        }

        return new DefaultProjectSnapshotProjectEngineFactory(_fallback, _factories);
    }
}
