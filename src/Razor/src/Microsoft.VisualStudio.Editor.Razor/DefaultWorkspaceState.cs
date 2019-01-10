// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.Editor.Razor
{
    internal class DefaultWorkspaceState : WorkspaceState
    {
        public DefaultWorkspaceState(
            Workspace workspace,
            ProjectSnapshotManager projectSnapshotManager,
            ImportDocumentManager importDocumentManager)
        {
            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (projectSnapshotManager == null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManager));
            }

            if (importDocumentManager == null)
            {
                throw new ArgumentNullException(nameof(importDocumentManager));
            }

            Workspace = workspace;
            ProjectSnapshotManager = projectSnapshotManager;
            ImportDocumentManager = importDocumentManager;
        }

        public override Workspace Workspace { get; }

        public override ProjectSnapshotManager ProjectSnapshotManager { get; }

        public override ImportDocumentManager ImportDocumentManager { get; }
    }
}
