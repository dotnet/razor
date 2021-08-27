// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    [Shared]
    [Export(typeof(ProjectSnapshotChangeTrigger))]
    internal class WorkspaceProjectStateChangeDetector : ProjectSnapshotChangeTrigger, IDisposable
    {
        private static readonly TimeSpan s_batchingDelay = TimeSpan.FromSeconds(1);
        private readonly object _disposeLock = new();
        private readonly ProjectWorkspaceStateGenerator _workspaceStateGenerator;
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private BatchingWorkQueue? _workQueue;
        private ProjectSnapshotManagerBase? _projectManager;
        private bool _disposed;

        private ProjectSnapshotManagerBase ProjectSnapshotManager
        {
            get
            {
                if (_projectManager is null)
                {
                    throw new InvalidOperationException($"ProjectManager was accessed before Initialize was called");
                }

                return _projectManager;
            }
        }

        [ImportingConstructor]
        public WorkspaceProjectStateChangeDetector(
            ProjectWorkspaceStateGenerator workspaceStateGenerator,
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher)
        {
            if (workspaceStateGenerator is null)
            {
                throw new ArgumentNullException(nameof(workspaceStateGenerator));
            }

            if (projectSnapshotManagerDispatcher is null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            }

            _workspaceStateGenerator = workspaceStateGenerator;
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
        }

        // Internal for testing
        internal WorkspaceProjectStateChangeDetector(
            ProjectWorkspaceStateGenerator workspaceStateGenerator,
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            BatchingWorkQueue workQueue)
        {
            _workspaceStateGenerator = workspaceStateGenerator;
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _workQueue = workQueue;
        }

        public ManualResetEventSlim? NotifyWorkspaceChangedEventComplete { get; set; }

        public override void Initialize(ProjectSnapshotManagerBase projectManager)
        {
            lock (_disposeLock)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(WorkspaceProjectStateChangeDetector));
                }

                // Can be non-null only in tests
                if (_workQueue == null)
                {
                    var errorReporter = projectManager.Workspace.Services.GetRequiredService<ErrorReporter>();
                    _workQueue = new BatchingWorkQueue(
                       s_batchingDelay,
                       FilePathComparer.Instance,
                       errorReporter);
                }
            }

            _projectManager = projectManager;
            _projectManager.Changed += ProjectManager_Changed;
            _projectManager.Workspace.WorkspaceChanged += Workspace_WorkspaceChanged;

            // This will usually no-op, in the case that another project snapshot change trigger immediately adds projects we want to be able to handle those projects
            InitializeSolution(_projectManager.Workspace.CurrentSolution);
        }

        // Internal for testing, virtual for temporary VSCode workaround
#pragma warning disable VSTHRD100 // Avoid async void methods
        internal async virtual void Workspace_WorkspaceChanged(object sender, WorkspaceChangeEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            try
            {
                // Method needs to be run on the project snapshot manager's specialized thread
                // due to project snapshot manager access.
                await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
                {
                    Project project;
                    switch (e.Kind)
                    {
                        case WorkspaceChangeKind.ProjectAdded:
                            {
                                project = e.NewSolution.GetRequiredProject(e.ProjectId);

                                EnqueueUpdateOnProjectAndDependencies(project, e.NewSolution);
                                break;
                            }

                        case WorkspaceChangeKind.ProjectChanged:
                        case WorkspaceChangeKind.ProjectReloaded:
                            {
                                project = e.NewSolution.GetRequiredProject(e.ProjectId);

                                EnqueueUpdateOnProjectAndDependencies(project, e.NewSolution);
                                break;
                            }

                        case WorkspaceChangeKind.ProjectRemoved:
                            {
                                project = e.OldSolution.GetRequiredProject(e.ProjectId);

                                if (TryGetProjectSnapshot(project.FilePath, out var projectSnapshot))
                                {
                                    EnqueueUpdateOnProjectAndDependencies(e.ProjectId!, project: null, e.OldSolution, projectSnapshot!);
                                }
                                break;
                            }

                        case WorkspaceChangeKind.DocumentAdded:
                            // This is the case when a component declaration file changes on disk. We have an MSBuild
                            // generator configured by the SDK that will poke these files on disk when a component
                            // is saved, or loses focus in the editor.
                            project = e.NewSolution.GetRequiredProject(e.ProjectId);
                            var newDocument = project.GetDocument(e.DocumentId!);

                            if (newDocument?.FilePath is null)
                            {
                                break;
                            }

                            if (IsRazorFileOrRazorVirtual(newDocument))
                            {
                                EnqueueUpdateOnProjectAndDependencies(project, e.NewSolution);
                                break;
                            }

                            // We now know we're not operating directly on a Razor file. However, it's possible the user
                            // is operating on a partial class that is associated with a Razor file.
                            if (IsPartialComponentClass(newDocument))
                            {
                                EnqueueUpdateOnProjectAndDependencies(project, e.NewSolution);
                            }

                            break;
                        case WorkspaceChangeKind.DocumentRemoved:
                            project = e.OldSolution.GetRequiredProject(e.ProjectId);
                            var removedDocument = project.GetRequiredDocument(e.DocumentId);

                            if (removedDocument.FilePath is null)
                            {
                                break;
                            }

                            if (IsRazorFileOrRazorVirtual(removedDocument))
                            {
                                EnqueueUpdateOnProjectAndDependencies(project, e.NewSolution);
                                break;
                            }

                            // We now know we're not operating directly on a Razor file. However, it's possible the user
                            // is operating on a partial class that is associated with a Razor file.

                            if (IsPartialComponentClass(removedDocument))
                            {
                                EnqueueUpdateOnProjectAndDependencies(project, e.NewSolution);
                            }

                            break;
                        case WorkspaceChangeKind.DocumentChanged:
                        case WorkspaceChangeKind.DocumentReloaded:
                            {
                                // This is the case when a component declaration file changes on disk. We have an MSBuild
                                // generator configured by the SDK that will poke these files on disk when a component
                                // is saved, or loses focus in the editor.
                                project = e.OldSolution.GetRequiredProject(e.ProjectId);
                                var document = project.GetRequiredDocument(e.DocumentId);

                                if (document.FilePath == null)
                                {
                                    break;
                                }

                                if (IsRazorFileOrRazorVirtual(document))
                                {
                                    var newProject = e.NewSolution.GetRequiredProject(e.ProjectId);
                                    EnqueueUpdateOnProjectAndDependencies(newProject, e.NewSolution);
                                    break;
                                }

                                // We now know we're not operating directly on a Razor file. However, it's possible the user is operating on a partial class that is associated with a Razor file.

                                if (IsPartialComponentClass(document))
                                {
                                    var newProject = e.NewSolution.GetRequiredProject(e.ProjectId);
                                    EnqueueUpdateOnProjectAndDependencies(newProject, e.NewSolution);
                                }

                                break;
                            }

                        case WorkspaceChangeKind.SolutionAdded:
                        case WorkspaceChangeKind.SolutionChanged:
                        case WorkspaceChangeKind.SolutionCleared:
                        case WorkspaceChangeKind.SolutionReloaded:
                        case WorkspaceChangeKind.SolutionRemoved:

                            if (e.OldSolution != null)
                            {
                                foreach (var p in e.OldSolution.Projects)
                                {
                                    if (TryGetProjectSnapshot(p?.FilePath, out var projectSnapshot))
                                    {
                                        EnqueueUpdate(project: null, projectSnapshot!);
                                    }
                                }
                            }

                            InitializeSolution(e.NewSolution);
                            break;
                    }

                    // Let tests know that this event has completed
                    if (NotifyWorkspaceChangedEventComplete != null)
                    {
                        NotifyWorkspaceChangedEventComplete.Set();
                    }
                }, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.Fail("WorkspaceProjectStateChangeDetector.Workspace_WorkspaceChanged threw exception:" +
                    Environment.NewLine + ex.Message + Environment.NewLine + "Stack trace:" + Environment.NewLine + ex.StackTrace);
            }

            static bool IsRazorFileOrRazorVirtual(Document document)
            {
                // Using EndsWith because Path.GetExtension will ignore everything before .cs
                // Using Ordinal because the SDK generates these filenames.
                // Stll have .cshtml.g.cs and .razor.g.cs for Razor.VSCode scenarios.
                return document.FilePath.EndsWith(".cshtml.g.cs", FilePathComparison.Instance) ||
                    document.FilePath.EndsWith(".razor.g.cs", FilePathComparison.Instance) ||
                    document.FilePath.EndsWith(".razor", FilePathComparison.Instance) ||

                    // VSCode's background C# document
                    document.FilePath.EndsWith("__bg__virtual.cs", StringComparison.Ordinal);
            }
        }

        // Internal for testing
        internal bool IsPartialComponentClass(Document document)
        {
            if (!document.TryGetSyntaxRoot(out var root))
            {
                return false;
            }

            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            if (!classDeclarations.Any())
            {
                return false;
            }

            if (!document.TryGetSemanticModel(out var semanticModel))
            {
                // This will occasionally return false resulting in us not refreshing TagHelpers for component partial classes. This means there are situations when a user's
                // TagHelper definitions will not immediately update but we will eventually acheive omniscience.
                return false;
            }

            var icomponentType = semanticModel.Compilation.GetTypeByMetadataName(ComponentsApi.IComponent.MetadataName);
            if (icomponentType == null)
            {
                // IComponent is not available in the compilation.
                return false;
            }

            foreach (var classDeclaration in classDeclarations)
            {
                if (semanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol classSymbol)
                {
                    continue;
                }

                if (ComponentDetectionConventions.IsComponent(classSymbol, icomponentType))
                {
                    return true;
                }
            }

            return false;
        }

        // Virtual for temporary VSCode workaround
        protected virtual void InitializeSolution(Solution solution)
        {
            foreach (var project in solution.Projects)
            {
                if (TryGetProjectSnapshot(project?.FilePath, out var projectSnapshot))
                {
                    EnqueueUpdate(project, projectSnapshot!);
                }
            }
        }

        private void ProjectManager_Changed(object sender, ProjectChangeEventArgs args)
        {
            // Don't do any work if the solution is closing. Any work in the queue will be cancelled on disposal
            if (args.SolutionIsClosing)
            {
                return;
            }

            switch (args.Kind)
            {
                case ProjectChangeKind.ProjectAdded:
                case ProjectChangeKind.DocumentRemoved:
                case ProjectChangeKind.DocumentAdded:
                    var associatedWorkspaceProject = ProjectSnapshotManager
                        .Workspace
                        .CurrentSolution
                        .Projects
                        .FirstOrDefault(project => FilePathComparer.Instance.Equals(args.ProjectFilePath, project.FilePath));

                    if (associatedWorkspaceProject != null)
                    {
                        var projectSnapshot = args.Newer;
                        EnqueueUpdateOnProjectAndDependencies(associatedWorkspaceProject.Id, associatedWorkspaceProject, associatedWorkspaceProject.Solution, projectSnapshot);
                    }
                    break;
            }
        }

        private void EnqueueUpdateOnProjectAndDependencies(Project project, Solution solution)
        {
            if (TryGetProjectSnapshot(project.FilePath, out var projectSnapshot))
            {
                EnqueueUpdateOnProjectAndDependencies(project.Id, project, solution, projectSnapshot!);
            }
        }

        private void EnqueueUpdateOnProjectAndDependencies(ProjectId projectId, Project? project, Solution solution, ProjectSnapshot projectSnapshot)
        {
            EnqueueUpdate(project, projectSnapshot);

            var dependencyGraph = solution.GetProjectDependencyGraph();

            var dependentProjectIds = dependencyGraph.GetProjectsThatTransitivelyDependOnThisProject(projectId);
            foreach (var dependentProjectId in dependentProjectIds)
            {
                var dependentProject = solution.GetProject(dependentProjectId);

                if (TryGetProjectSnapshot(dependentProject?.FilePath, out var dependentProjectSnapshot))
                {
                    EnqueueUpdate(dependentProject, dependentProjectSnapshot!);
                }
            }
        }

        private void EnqueueUpdate(Project? project, ProjectSnapshot projectSnapshot)
        {
            var workItem = new UpdateWorkspaceWorkItem(project, projectSnapshot, _workspaceStateGenerator, _projectSnapshotManagerDispatcher);
            lock (_disposeLock)
            {
                _workQueue?.Enqueue(projectSnapshot.FilePath, workItem);
            }
        }

        // TODO: [NotNullWhen] here when we move to netstandard2.1
        private bool TryGetProjectSnapshot(string? projectFilePath, out ProjectSnapshot? projectSnapshot)
        {
            if (projectFilePath is null)
            {
                projectSnapshot = null;
                return false;
            }

            projectSnapshot = ProjectSnapshotManager.GetLoadedProject(projectFilePath);
            return projectSnapshot != null;
        }

        public void Dispose()
        {
            lock (_disposeLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _workQueue?.Dispose();
                _workQueue = null;
            }
        }

        private class UpdateWorkspaceWorkItem : BatchableWorkItem
        {
            private readonly Project? _workspaceProject;
            private readonly ProjectSnapshot _projectSnapshot;
            private readonly ProjectWorkspaceStateGenerator _workspaceStateGenerator;
            private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;

            public UpdateWorkspaceWorkItem(
                Project? workspaceProject,
                ProjectSnapshot projectSnapshot,
                ProjectWorkspaceStateGenerator workspaceStateGenerator,
                ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher)
            {
                if (projectSnapshot is null)
                {
                    throw new ArgumentNullException(nameof(projectSnapshot));
                }

                if (workspaceStateGenerator is null)
                {
                    throw new ArgumentNullException(nameof(workspaceStateGenerator));
                }

                if (projectSnapshotManagerDispatcher is null)
                {
                    throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
                }

                _workspaceProject = workspaceProject;
                _projectSnapshot = projectSnapshot;
                _workspaceStateGenerator = workspaceStateGenerator;
                _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            }

            public override ValueTask ProcessAsync(CancellationToken cancellationToken)
            {
                var task = _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
                {
                    _workspaceStateGenerator.Update(_workspaceProject, _projectSnapshot, cancellationToken);
                }, cancellationToken);
                return new ValueTask(task);
            }
        }
    }
}
