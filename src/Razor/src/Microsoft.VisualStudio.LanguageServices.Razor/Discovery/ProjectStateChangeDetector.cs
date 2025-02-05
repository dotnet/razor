// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudio.Razor.Discovery;

[Export(typeof(IRazorStartupService))]
internal partial class ProjectStateChangeDetector : IRazorStartupService, IDisposable
{
    private readonly record struct Work(ProjectKey Key, ProjectId? Id);

    private static readonly TimeSpan s_delay = TimeSpan.FromSeconds(1);

    private readonly IProjectStateUpdater _generator;
    private readonly ProjectSnapshotManager _projectManager;
    private readonly LanguageServerFeatureOptions _options;
    private readonly CodeAnalysis.Workspace _workspace;

    private readonly CancellationTokenSource _disposeTokenSource;
    private readonly AsyncBatchingWorkQueue<Work> _workQueue;

    private WorkspaceChangedListener? _workspaceChangedListener;

    [ImportingConstructor]
    public ProjectStateChangeDetector(
        IProjectStateUpdater generator,
        ProjectSnapshotManager projectManager,
        LanguageServerFeatureOptions options,
        IWorkspaceProvider workspaceProvider)
        : this(generator, projectManager, options, workspaceProvider, s_delay)
    {
    }

    public ProjectStateChangeDetector(
        IProjectStateUpdater generator,
        ProjectSnapshotManager projectManager,
        LanguageServerFeatureOptions options,
        IWorkspaceProvider workspaceProvider,
        TimeSpan delay)
    {
        _generator = generator;
        _projectManager = projectManager;
        _options = options;

        _disposeTokenSource = new();
        _workQueue = new AsyncBatchingWorkQueue<Work>(
            delay,
            ProcessBatchAsync,
            _disposeTokenSource.Token);

        _projectManager.Changed += ProjectManager_Changed;

        _workspace = workspaceProvider.GetWorkspace();
        _workspace.WorkspaceChanged += Workspace_WorkspaceChanged;

        // This will usually no-op, in the case that another project snapshot change trigger
        // immediately adds projects we want to be able to handle those projects.
        InitializeSolution(_workspace.CurrentSolution);
    }

    public void Dispose()
    {
        if (_disposeTokenSource.IsCancellationRequested)
        {
            return;
        }

        _projectManager.Changed -= ProjectManager_Changed;
        _workspace.WorkspaceChanged -= Workspace_WorkspaceChanged;

        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();
    }

    private ValueTask ProcessBatchAsync(ImmutableArray<Work> items, CancellationToken token)
    {
        foreach (var (projectKey, projectId) in items.GetMostRecentUniqueItems(Comparer.Instance))
        {
            if (token.IsCancellationRequested)
            {
                return default;
            }

            _generator.EnqueueUpdate(projectKey, projectId);
        }

        return default;
    }

    private void Workspace_WorkspaceChanged(object? sender, WorkspaceChangeEventArgs e)
    {
        switch (e.Kind)
        {
            case WorkspaceChangeKind.ProjectAdded:
            case WorkspaceChangeKind.ProjectChanged:
            case WorkspaceChangeKind.ProjectReloaded:
                {
                    var projectId = e.ProjectId.AssumeNotNull();
                    var newSolution = e.NewSolution;

                    var project = newSolution.GetRequiredProject(projectId);
                    EnqueueUpdateOnProjectAndDependencies(project, newSolution);
                }

                break;

            case WorkspaceChangeKind.ProjectRemoved:
                {
                    var projectId = e.ProjectId.AssumeNotNull();
                    var oldSolution = e.OldSolution;

                    var project = oldSolution.GetRequiredProject(projectId);

                    if (TryGetProjectSnapshot(project, out var projectSnapshot))
                    {
                        EnqueueUpdateOnProjectAndDependencies(projectId, project: null, oldSolution, projectSnapshot);
                    }
                }

                break;

            case WorkspaceChangeKind.DocumentAdded:
                {
                    var projectId = e.ProjectId.AssumeNotNull();
                    var documentId = e.DocumentId.AssumeNotNull();
                    var newSolution = e.NewSolution;

                    // This is the case when a component declaration file changes on disk. We have an MSBuild
                    // generator configured by the SDK that will poke these files on disk when a component
                    // is saved, or loses focus in the editor.
                    var project = newSolution.GetRequiredProject(projectId);

                    if (project.GetDocument(documentId) is not Document newDocument ||
                        newDocument.FilePath is null)
                    {
                        return;
                    }

                    if (IsRazorFileOrRazorVirtual(newDocument))
                    {
                        EnqueueUpdateOnProjectAndDependencies(project, newSolution);
                        return;
                    }

                    // We now know we're not operating directly on a Razor file. However, it's possible the user
                    // is operating on a partial class that is associated with a Razor file.
                    if (IsPartialComponentClass(newDocument))
                    {
                        EnqueueUpdateOnProjectAndDependencies(project, newSolution);
                    }
                }

                break;

            case WorkspaceChangeKind.DocumentRemoved:
                {
                    var projectId = e.ProjectId.AssumeNotNull();
                    var documentId = e.DocumentId.AssumeNotNull();
                    var oldSolution = e.OldSolution;
                    var newSolution = e.NewSolution;

                    var project = oldSolution.GetRequiredProject(projectId);
                    var removedDocument = project.GetRequiredDocument(documentId);

                    if (removedDocument.FilePath is null)
                    {
                        return;
                    }

                    if (IsRazorFileOrRazorVirtual(removedDocument))
                    {
                        EnqueueUpdateOnProjectAndDependencies(project, newSolution);
                        return;
                    }

                    // We now know we're not operating directly on a Razor file. However, it's possible the user
                    // is operating on a partial class that is associated with a Razor file.

                    if (IsPartialComponentClass(removedDocument))
                    {
                        EnqueueUpdateOnProjectAndDependencies(project, newSolution);
                    }
                }

                break;

            case WorkspaceChangeKind.DocumentChanged:
            case WorkspaceChangeKind.DocumentReloaded:
                {
                    var projectId = e.ProjectId.AssumeNotNull();
                    var documentId = e.DocumentId.AssumeNotNull();
                    var oldSolution = e.OldSolution;
                    var newSolution = e.NewSolution;

                    // This is the case when a component declaration file changes on disk. We have an MSBuild
                    // generator configured by the SDK that will poke these files on disk when a component
                    // is saved, or loses focus in the editor.
                    var project = oldSolution.GetRequiredProject(projectId);
                    var document = project.GetRequiredDocument(documentId);

                    if (document.FilePath is null)
                    {
                        return;
                    }

                    if (IsRazorFileOrRazorVirtual(document))
                    {
                        var newProject = newSolution.GetRequiredProject(projectId);
                        EnqueueUpdateOnProjectAndDependencies(newProject, newSolution);
                        return;
                    }

                    // We now know we're not operating directly on a Razor file. However, it's possible the user
                    // is operating on a partial class that is associated with a Razor file.
                    if (IsPartialComponentClass(document))
                    {
                        var newProject = newSolution.GetRequiredProject(projectId);
                        EnqueueUpdateOnProjectAndDependencies(newProject, newSolution);
                    }
                }

                break;

            case WorkspaceChangeKind.SolutionAdded:
            case WorkspaceChangeKind.SolutionChanged:
            case WorkspaceChangeKind.SolutionCleared:
            case WorkspaceChangeKind.SolutionReloaded:
            case WorkspaceChangeKind.SolutionRemoved:
                {
                    var oldSolution = e.OldSolution;
                    var newSolution = e.NewSolution;

                    foreach (var project in oldSolution.Projects)
                    {
                        if (TryGetProjectSnapshot(project, out var projectSnapshot))
                        {
                            EnqueueUpdate(project: null, projectSnapshot);
                        }
                    }

                    InitializeSolution(newSolution);
                }

                break;
        }

        _workspaceChangedListener?.WorkspaceChanged(e.Kind);
    }

    private bool IsRazorFileOrRazorVirtual(Document document)
    {
        if (document.FilePath is not { } filePath)
            return false;

        // Using EndsWith because Path.GetExtension will ignore everything before .cs
        return filePath.EndsWith(_options.CSharpVirtualDocumentSuffix, FilePathComparison.Instance) ||
               // Still have .cshtml.g.cs and .razor.g.cs for Razor.VSCode scenarios.
               filePath.EndsWith(".cshtml.g.cs", FilePathComparison.Instance) ||
               filePath.EndsWith(".razor.g.cs", FilePathComparison.Instance) ||
               filePath.EndsWith(".razor", FilePathComparison.Instance) ||

               // VSCode's background C# document
               // Using Ordinal because the SDK generates these filenames.
               filePath.EndsWith("__bg__virtual.cs", StringComparison.Ordinal);
    }

    // Internal for testing
    internal static bool IsPartialComponentClass(Document document)
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
            // This will occasionally return false resulting in us not refreshing TagHelpers
            // for component partial classes. This means there are situations when a user's
            // TagHelper definitions will not immediately update but we will eventually achieve omniscience.
            return false;
        }

        var icomponentType = semanticModel.Compilation.GetTypeByMetadataName(ComponentsApi.IComponent.MetadataName);
        if (icomponentType is null)
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

    private void InitializeSolution(Solution solution)
    {
        foreach (var project in solution.Projects)
        {
            if (TryGetProjectSnapshot(project, out var projectSnapshot))
            {
                EnqueueUpdate(project, projectSnapshot);
            }
        }
    }

    private void ProjectManager_Changed(object? sender, ProjectChangeEventArgs e)
    {
        // Don't do any work if the solution is closing. Any work in the queue will be cancelled on disposal
        if (e.IsSolutionClosing)
        {
            _workQueue.CancelExistingWork();
            return;
        }

        switch (e.Kind)
        {
            case ProjectChangeKind.ProjectAdded:
            case ProjectChangeKind.DocumentRemoved:
            case ProjectChangeKind.DocumentAdded:
                var solution = _workspace.CurrentSolution;

                if (solution.TryGetProject(e.ProjectKey, out var workspaceProject))
                {
                    var projectSnapshot = e.Newer.AssumeNotNull();

                    EnqueueUpdateOnProjectAndDependencies(
                        workspaceProject.Id,
                        workspaceProject,
                        solution,
                        projectSnapshot);
                }

                break;

            case ProjectChangeKind.ProjectRemoved:
                // No-op. We don't need to recompute tag helpers if the project is being removed
                break;
        }
    }

    private void EnqueueUpdateOnProjectAndDependencies(Project project, Solution solution)
    {
        if (TryGetProjectSnapshot(project, out var projectSnapshot))
        {
            EnqueueUpdateOnProjectAndDependencies(project.Id, project, solution, projectSnapshot);
        }
    }

    private void EnqueueUpdateOnProjectAndDependencies(ProjectId projectId, Project? project, Solution solution, ProjectSnapshot projectSnapshot)
    {
        EnqueueUpdate(project, projectSnapshot);

        var dependencyGraph = solution.GetProjectDependencyGraph();
        var dependentProjectIds = dependencyGraph.GetProjectsThatTransitivelyDependOnThisProject(projectId);

        foreach (var dependentProjectId in dependentProjectIds)
        {
            if (solution.GetProject(dependentProjectId) is { } dependentProject &&
                TryGetProjectSnapshot(dependentProject, out var dependentProjectSnapshot))
            {
                EnqueueUpdate(dependentProject, dependentProjectSnapshot);
            }
        }
    }

    private void EnqueueUpdate(Project? project, ProjectSnapshot projectSnapshot)
    {
        _workQueue.AddWork(new Work(projectSnapshot.Key, project?.Id));
    }

    private bool TryGetProjectSnapshot(Project? project, [NotNullWhen(true)] out ProjectSnapshot? projectSnapshot)
    {
        if (project?.CompilationOutputInfo.AssemblyPath is null)
        {
            projectSnapshot = null;
            return false;
        }

        var projectKey = project.ToProjectKey();

        return _projectManager.TryGetProject(projectKey, out projectSnapshot);
    }
}
