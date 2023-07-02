// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

[Export(typeof(IUnconfiguredProjectCommonServices))]
internal class UnconfiguredProjectCommonServices : IUnconfiguredProjectCommonServices
{
    [ImportingConstructor]
    public UnconfiguredProjectCommonServices(
        [Import(ExportContractNames.Scopes.UnconfiguredProject)] IProjectAsynchronousTasksService tasksService,
        IProjectThreadingService threadingService,
        UnconfiguredProject unconfiguredProject,
        IActiveConfiguredProjectSubscriptionService activeConfiguredProjectSubscription)
    {
        if (tasksService is null)
        {
            throw new ArgumentNullException(nameof(tasksService));
        }

        if (threadingService is null)
        {
            throw new ArgumentNullException(nameof(threadingService));
        }

        if (unconfiguredProject is null)
        {
            throw new ArgumentNullException(nameof(unconfiguredProject));
        }

        if (activeConfiguredProjectSubscription is null)
        {
            throw new ArgumentNullException(nameof(activeConfiguredProjectSubscription));
        }

        TasksService = tasksService;
        ThreadingService = threadingService;
        UnconfiguredProject = unconfiguredProject;
        ActiveConfiguredProjectSubscription = activeConfiguredProjectSubscription;
    }

    public IActiveConfiguredProjectSubscriptionService ActiveConfiguredProjectSubscription { get; }

    public IProjectAsynchronousTasksService TasksService { get; }

    public IProjectThreadingService ThreadingService { get; }

    public UnconfiguredProject UnconfiguredProject { get; }
}
