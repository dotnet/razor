// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Moq;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    internal class TestProjectSnapshotManager : DefaultProjectSnapshotManager
    {
        List<string> OpenDocuments = new List<string>();

        public TestProjectSnapshotManager(Workspace workspace)
            : base(Mock.Of<ForegroundDispatcher>(), Mock.Of<ErrorReporter>(), Enumerable.Empty<ProjectSnapshotChangeTrigger>(), workspace)
        {
        }

        public TestProjectSnapshotManager(ForegroundDispatcher foregroundDispatcher, Workspace workspace)
            : base(foregroundDispatcher, Mock.Of<ErrorReporter>(), Enumerable.Empty<ProjectSnapshotChangeTrigger>(), workspace)
        {
        }

        public bool AllowNotifyListeners { get; set; }

        public DefaultProjectSnapshot GetSnapshot(HostProject hostProject)
        {
            return Projects.Cast<DefaultProjectSnapshot>().FirstOrDefault(s => s.FilePath == hostProject.FilePath);
        }

        public void MarkDocumentAsOpen(string documentFilePath)
        {
            OpenDocuments.Add(documentFilePath);
        }

        public override bool IsDocumentOpen(string documentFilePath)
        {
            return OpenDocuments.Contains(documentFilePath);
        }

        public DefaultProjectSnapshot GetSnapshot(Project workspaceProject)
        {
            return Projects.Cast<DefaultProjectSnapshot>().FirstOrDefault(s => s.FilePath == workspaceProject.FilePath);
        }

        protected override void NotifyListeners(ProjectChangeEventArgs e)
        {
            if (AllowNotifyListeners)
            {
                base.NotifyListeners(e);
            }
        }
    }
}
