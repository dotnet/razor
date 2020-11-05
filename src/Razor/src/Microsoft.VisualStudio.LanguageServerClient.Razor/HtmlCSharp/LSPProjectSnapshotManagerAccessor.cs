// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [Export(typeof(ProjectSnapshotChangeTrigger))]
    [Export(typeof(LSPProjectSnapshotManagerAccessor))]
    internal class LSPProjectSnapshotManagerAccessor : ProjectSnapshotChangeTrigger
    {
        internal ProjectSnapshotManagerBase ProjectSnapshotManager { get; set; }

        public override void Initialize(ProjectSnapshotManagerBase projectManager)
        {
            ProjectSnapshotManager = projectManager;
        }
    }
}
