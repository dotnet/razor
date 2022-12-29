// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.OmniSharp.Project;

internal class OmniSharpProjectWorkspaceStateGenerator : AbstractOmniSharpProjectSnapshotManagerChangeTrigger
{
    // Internal for testing
    internal OmniSharpProjectWorkspaceStateGenerator()
    {
    }

    public OmniSharpProjectWorkspaceStateGenerator(OmniSharpProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher)
    {
        if (projectSnapshotManagerDispatcher is null)
        {
            throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
        }

        InternalWorkspaceStateGenerator = new DefaultProjectWorkspaceStateGenerator(projectSnapshotManagerDispatcher.InternalDispatcher);
    }

    internal DefaultProjectWorkspaceStateGenerator InternalWorkspaceStateGenerator { get; }

    internal override void Initialize(OmniSharpProjectSnapshotManagerBase projectManager) => InternalWorkspaceStateGenerator.Initialize(projectManager.InternalProjectSnapshotManager);

    internal virtual void Update(CodeAnalysis.Project workspaceProject, OmniSharpProjectSnapshot projectSnapshot) => InternalWorkspaceStateGenerator.Update(workspaceProject, projectSnapshot.InternalProjectSnapshot, CancellationToken.None);
}
