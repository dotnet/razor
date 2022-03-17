// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Editor.Razor.Documents;

namespace Microsoft.VisualStudio.Mac.LanguageServices.Razor
{
    [Shared]
    [ExportWorkspaceServiceFactory(typeof(FileChangeTrackerFactory), ServiceLayer.Host)]
    internal class VisualStudioMacFileChangeTrackerFactoryFactory : IWorkspaceServiceFactory
    {
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;

        [ImportingConstructor]
        public VisualStudioMacFileChangeTrackerFactoryFactory(ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher!!)
        {
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
        }

        public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices!!)
        {
            return new VisualStudioMacFileChangeTrackerFactory(_projectSnapshotManagerDispatcher);
        }
    }
}
