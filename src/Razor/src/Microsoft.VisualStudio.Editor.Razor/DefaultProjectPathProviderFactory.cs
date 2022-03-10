// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;

namespace Microsoft.VisualStudio.Editor.Razor
{
    [Shared]
    [ExportWorkspaceServiceFactory(typeof(ProjectPathProvider), ServiceLayer.Default)]
    internal class DefaultProjectPathProviderFactory : IWorkspaceServiceFactory
    {
        private readonly TextBufferProjectService _projectService;
        private readonly LiveShareProjectPathProvider _liveShareProjectPathProvider;

        [ImportingConstructor]
        public DefaultProjectPathProviderFactory(
            TextBufferProjectService projectService!!,
            [Import(AllowDefault = true)] LiveShareProjectPathProvider liveShareProjectPathProvider)
        {
            _projectService = projectService;
            _liveShareProjectPathProvider = liveShareProjectPathProvider;
        }

        public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices!!)
        {
            return new DefaultProjectPathProvider(_projectService, _liveShareProjectPathProvider);
        }
    }
}
