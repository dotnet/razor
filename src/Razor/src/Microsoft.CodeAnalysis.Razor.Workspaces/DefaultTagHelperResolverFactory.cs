﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;

namespace Microsoft.CodeAnalysis.Razor
{
    [Shared]
    [ExportWorkspaceServiceFactory(typeof(TagHelperResolver), ServiceLayer.Default)]
    internal class DefaultTagHelperResolverFactory : IWorkspaceServiceFactory
    {
        public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices)
        {
            return new DefaultTagHelperResolver();
        }
    }
}
