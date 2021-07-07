// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.AspNetCore.Razor.OmniSharpPlugin.StrongNamed
{
    public class OmniSharpProjectWorkspaceStateGenerator : IOmniSharpProjectSnapshotManagerChangeTrigger
    {
        // Internal for testing
        internal OmniSharpProjectWorkspaceStateGenerator()
        {
        }

        public OmniSharpProjectWorkspaceStateGenerator(OmniSharpSingleThreadedDispatcher singleThreadedDispatcher)
        {
            if (singleThreadedDispatcher == null)
            {
                throw new ArgumentNullException(nameof(singleThreadedDispatcher));
            }

            InternalWorkspaceStateGenerator = new DefaultProjectWorkspaceStateGenerator(singleThreadedDispatcher.InternalDispatcher);
        }

        internal DefaultProjectWorkspaceStateGenerator InternalWorkspaceStateGenerator { get; }

        public void Initialize(OmniSharpProjectSnapshotManagerBase projectManager) => InternalWorkspaceStateGenerator.Initialize(projectManager.InternalProjectSnapshotManager);

        public virtual void Update(Project workspaceProject, OmniSharpProjectSnapshot projectSnapshot) => InternalWorkspaceStateGenerator.Update(workspaceProject, projectSnapshot.InternalProjectSnapshot, CancellationToken.None);
    }
}
