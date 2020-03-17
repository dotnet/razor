// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    internal abstract class RazorProjectChangePublisher : ProjectSnapshotChangeTrigger
    {
        private ProjectSnapshotManagerBase _projectSnapshotManager;
        protected readonly ConcurrentDictionary<string, string> PublishFilePathMappings;

        public RazorProjectChangePublisher()
        {
            PublishFilePathMappings = new ConcurrentDictionary<string, string>(FilePathComparer.Instance);
        }

        public override void Initialize(ProjectSnapshotManagerBase projectManager)
        {
            _projectSnapshotManager = projectManager;
            _projectSnapshotManager.Changed += ProjectSnapshotManager_Changed;
        }

        internal abstract void ProjectSnapshotManager_Changed(object sender, ProjectChangeEventArgs e);

        // We need this because we want to be able to check that we're on the main thread,
        // but we don't want to use JoinableTaskContext because it would require
        // adding `Microsoft.VisualStudio.Threading` to this project.
        protected abstract bool IsOnMainThread();

        public virtual void SetPublishFilePath(string projectFilePath, string publishFilePath)
        {
            // Should only be called from the main thread.
            Debug.Assert(IsOnMainThread(), "SetPublishFilePath should have been on main thread");
            PublishFilePathMappings[projectFilePath] = publishFilePath;
        }

        public virtual void RemovePublishFilePath(string projectFilePath)
        {
            Debug.Assert(IsOnMainThread(), "RemovePublishFilePath should have been on main thread");
            PublishFilePathMappings.TryRemove(projectFilePath, out var _);
        }
    }
}
