// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.OmniSharp.Project;

public class OmniSharpProjectWorkspaceStateGenerator : AbstractOmniSharpProjectSnapshotManagerChangeTrigger
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

    public override void Initialize(OmniSharpProjectSnapshotManager projectManager) => InternalWorkspaceStateGenerator.Initialize(projectManager.InternalProjectSnapshotManager);

    public virtual void Update(CodeAnalysis.Project workspaceProject, OmniSharpProjectSnapshot projectSnapshot) => InternalWorkspaceStateGenerator.Update(workspaceProject, projectSnapshot.InternalProjectSnapshot, CancellationToken.None);
}
