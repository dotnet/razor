// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.ProjectEngineHost;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.LanguageServices.Razor;

[Export(typeof(IProjectSnapshotManagerAccessor))]
[method: ImportingConstructor]
internal sealed class VisualStudioProjectSnapshotManagerAccessor(
    [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider,
    IProjectEngineFactoryProvider projectEngineFactoryProvider,
    ProjectSnapshotManagerDispatcher dispatcher,
    IErrorReporter errorReporter,
    JoinableTaskContext joinableTaskContext)
    : IProjectSnapshotManagerAccessor
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IProjectEngineFactoryProvider _projectEngineFactoryProvider = projectEngineFactoryProvider;
    private readonly ProjectSnapshotManagerDispatcher _dispatcher = dispatcher;
    private readonly IErrorReporter _errorReporter = errorReporter;
    private readonly JoinableTaskFactory _jtf = joinableTaskContext.Factory;

    private ProjectSnapshotManagerBase? _projectManager;

    public ProjectSnapshotManagerBase Instance
    {
        get
        {
            if (_projectManager is { } projectManager)
            {
                return projectManager;
            }

            if (_dispatcher.IsRunningOnDispatcher)
            {
                return _projectManager ??= Create();
            }

            // The JTF.Run isn't great, but it should go away with IProjectSnapshotManagerAccessor.
            // ProjectSnapshotManager must be created on the dispatcher scheduler because it calls the
            // Initialize() method of any IProjectSnapshotChangeTrigger its created with.

            return _jtf.Run(async () =>
            {
                await _dispatcher.Scheduler;

                return _projectManager ??= Create();
            });

            ProjectSnapshotManagerBase Create()
            {
                _dispatcher.AssertRunningOnDispatcher();

                var projectManager = new DefaultProjectSnapshotManager(_projectEngineFactoryProvider, _dispatcher, _errorReporter);

                var componentModel = _serviceProvider.GetService<SComponentModel, IComponentModel>();
                var changeTriggers = componentModel.GetExtensions<IProjectSnapshotChangeTrigger>();

                projectManager.InitializeChangeTriggers(changeTriggers);

                return projectManager;
            }
        }
    }

    public bool TryGetInstance([NotNullWhen(true)] out ProjectSnapshotManagerBase? instance)
    {
        instance = _projectManager;
        return instance is not null;
    }
}
