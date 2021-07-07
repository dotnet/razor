// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
        private readonly SingleThreadedDispatcher _singleThreadedDispatcher;

        [ImportingConstructor]
        public VisualStudioMacFileChangeTrackerFactoryFactory(SingleThreadedDispatcher singleThreadedDispatcher)
        {
            if (singleThreadedDispatcher == null)
            {
                throw new ArgumentNullException(nameof(singleThreadedDispatcher));
            }

            _singleThreadedDispatcher = singleThreadedDispatcher;
        }

        public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices)
        {
            if (workspaceServices == null)
            {
                throw new ArgumentNullException(nameof(workspaceServices));
            }

            return new VisualStudioMacFileChangeTrackerFactory(_singleThreadedDispatcher);
        }
    }
}
