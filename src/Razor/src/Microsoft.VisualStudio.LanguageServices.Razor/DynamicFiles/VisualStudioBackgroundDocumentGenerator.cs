// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.Razor.DynamicFiles;

[Export(typeof(IRazorStartupService))]
internal class VisualStudioBackgroundDocumentGenerator : AbstractBackgroundDocumentGenerator
{
    private static readonly TimeSpan s_delay = TimeSpan.FromSeconds(2);

    [ImportingConstructor]
    public VisualStudioBackgroundDocumentGenerator(IProjectSnapshotManager projectManager, IRazorDynamicFileInfoProviderInternal infoProvider, ILoggerFactory loggerFactory)
        : this(projectManager, infoProvider, loggerFactory, s_delay)
    {
    }

    protected VisualStudioBackgroundDocumentGenerator(IProjectSnapshotManager projectManager, IRazorDynamicFileInfoProviderInternal infoProvider, ILoggerFactory loggerFactory, TimeSpan delay)
        : base(projectManager, infoProvider, loggerFactory, delay)
    {
    }

    protected override IDynamicDocumentContainer CreateContainer(IDocumentSnapshot documentSnapshot, ILoggerFactory loggerFactory)
        => new VisualStudioDynamicDocumentContainer(documentSnapshot, loggerFactory);

    protected override bool IgnoreEnqueue(IProjectSnapshot project, IDocumentSnapshot document)
    {
        if (project is ProjectSnapshot { HostProject: FallbackHostProject })
        {
            // We don't support closed file code generation for fallback projects
            return true;
        }

        return false;
    }
}
