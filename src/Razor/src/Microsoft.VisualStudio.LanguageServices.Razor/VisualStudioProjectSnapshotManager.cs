// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.LanguageServices.Razor;

[System.Composition.Shared]
[Export(typeof(ProjectSnapshotManager))]
internal sealed class VisualStudioProjectSnapshotManager : DefaultProjectSnapshotManager
{
    internal override Workspace Workspace { get; }

    public VisualStudioProjectSnapshotManager(
        IErrorReporter errorReporter,
        IEnumerable<IProjectSnapshotChangeTrigger> triggers,
        IProjectSnapshotProjectEngineFactory projectEngineFactory,
        ProjectSnapshotManagerDispatcher dispatcher,
        [Import(typeof(VisualStudioWorkspace))] Workspace workspace)
        : base(errorReporter, triggers, projectEngineFactory, dispatcher)
    {
        Workspace = workspace;
    }
}
