// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.SolutionRestoreManager;
using Xunit;
using IAsyncDisposable = System.IAsyncDisposable;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.Razor.Integration.Test.InProcess
{
    internal class SolutionExplorerInProcess : InProcComponent
    {
        public SolutionExplorerInProcess(TestServices testServices)
            : base(testServices)
        {
        }

        public async Task CreateSolutionAsync(string solutionName, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var solutionPath = CreateTemporaryPath();
            await CreateSolutionAsync(solutionPath, solutionName, cancellationToken);
        }

        private async Task CreateSolutionAsync(string solutionPath, string solutionName, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            await CloseSolutionAsync(cancellationToken);

            var solutionFileName = Path.ChangeExtension(solutionName, ".sln");
            Directory.CreateDirectory(solutionPath);

            // Make sure the shell debugger package is loaded so it doesn't try to load during the synchronous portion
            // of IVsSolution.CreateSolution.
            //
            // TODO: Identify the correct tracking bug
            _ = await GetRequiredGlobalServiceAsync<SVsShellDebugger, IVsDebugger>(cancellationToken);

            var solution = await GetRequiredGlobalServiceAsync<SVsSolution, IVsSolution>(cancellationToken);
            ErrorHandler.ThrowOnFailure(solution.CreateSolution(solutionPath, solutionFileName, (uint)__VSCREATESOLUTIONFLAGS.CSF_SILENT));
            ErrorHandler.ThrowOnFailure(solution.SaveSolutionElement((uint)__VSSLNSAVEOPTIONS.SLNSAVEOPT_ForceSave, null, 0));
        }

        public async Task AddProjectAsync(string projectName, string projectTemplate, string languageName, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var projectPath = Path.Combine(await GetDirectoryNameAsync(cancellationToken), projectName);
            var projectTemplatePath = await GetProjectTemplatePathAsync(projectTemplate, ConvertLanguageName(languageName), cancellationToken);
            var solution = await GetRequiredGlobalServiceAsync<SVsSolution, IVsSolution6>(cancellationToken);
            // TODO: How do we deal with the button
            ErrorHandler.ThrowOnFailure(solution.AddNewProjectFromTemplate(projectTemplatePath, null, null, projectPath, projectName, null, out _));
        }

        private async Task<string> GetProjectTemplatePathAsync(string projectTemplate, string languageName, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var dte = await GetRequiredGlobalServiceAsync<SDTE, EnvDTE.DTE>(cancellationToken);
            var solution = (EnvDTE80.Solution2)dte.Solution;

            if (string.Equals(languageName, "csharp", StringComparison.OrdinalIgnoreCase)
                && GetCSharpProjectTemplates().TryGetValue(projectTemplate, out var csharpProjectTemplate))
            {
                return solution.GetProjectTemplate(csharpProjectTemplate, languageName);
            }

            throw new NotImplementedException();

            static ImmutableDictionary<string, string> GetCSharpProjectTemplates()
            {
                var builder = ImmutableDictionary.CreateBuilder<string, string>();
                builder[WellKnownProjectTemplates.BlazorProject] = "BlazorTemplate";
                return builder.ToImmutable();
            }
        }

        public async Task RestoreNuGetPackagesAsync(CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var dte = await GetRequiredGlobalServiceAsync<SDTE, EnvDTE.DTE>(cancellationToken);
            var solution = (EnvDTE80.Solution2)dte.Solution;
            foreach (var project in solution.Projects.OfType<EnvDTE.Project>())
            {
                await RestoreNuGetPackagesAsync(project.FullName, cancellationToken);
            }
        }

        public async Task RestoreNuGetPackagesAsync(string projectName, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            await TestServices.Workspace.WaitForProjectSystemAsync(cancellationToken);

            var solutionRestoreService = await GetComponentModelServiceAsync<IVsSolutionRestoreService>(cancellationToken);
            await solutionRestoreService.CurrentRestoreOperation;

            var projectFullPath = (await GetProjectAsync(projectName, cancellationToken)).FullName;
            var solutionRestoreStatusProvider = await GetComponentModelServiceAsync<IVsSolutionRestoreStatusProvider>(cancellationToken);
            if (await solutionRestoreStatusProvider.IsRestoreCompleteAsync(cancellationToken))
            {
                return;
            }

            var solutionRestoreService2 = (IVsSolutionRestoreService2)solutionRestoreService;
            await solutionRestoreService2.NominateProjectAsync(projectFullPath, cancellationToken);

            // Check IsRestoreCompleteAsync until it returns true (this stops the retry because true != default(bool))
            await Helper.RetryAsync(
                cancellationToken => solutionRestoreStatusProvider.IsRestoreCompleteAsync(cancellationToken),
                TimeSpan.FromMilliseconds(50),
                cancellationToken);
        }

        public async Task OpenFileAsync(string projectName, string relativeFilePath, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var filePath = await GetAbsolutePathForProjectRelativeFilePathAsync(projectName, relativeFilePath, cancellationToken);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(filePath);
            }

            VsShellUtilities.OpenDocument(ServiceProvider.GlobalProvider, filePath, VSConstants.LOGVIEWID.Code_guid, out _, out _, out _, out var view);

            // Reliably set focus using NavigateToLineAndColumn
            var textManager = await GetRequiredGlobalServiceAsync<SVsTextManager, IVsTextManager>(cancellationToken);
            ErrorHandler.ThrowOnFailure(view.GetBuffer(out var textLines));
            ErrorHandler.ThrowOnFailure(view.GetCaretPos(out var line, out var column));
            ErrorHandler.ThrowOnFailure(textManager.NavigateToLineAndColumn(textLines, VSConstants.LOGVIEWID.Code_guid, line, column, line, column));
        }

        /// <summary>
        /// Add new file to project.
        /// </summary>
        /// <param name="projectName">The project that contains the file.</param>
        /// <param name="fileName">The name of the file to add.</param>
        /// <param name="contents">The contents of the file to overwrite. An empty file is create if null is passed.</param>
        /// <param name="open">Whether to open the file after it has been updated.</param>
        /// <param name="cancellationToken"></param>
        public async Task AddFileAsync(string projectName, string fileName, string? contents = null, bool open = false, CancellationToken cancellationToken = default)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var project = await GetProjectAsync(projectName, cancellationToken);
            var projectDirectory = Path.GetDirectoryName(project.FullName);
            var filePath = Path.Combine(projectDirectory, fileName);
            var directoryPath = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(directoryPath);

            if (contents != null)
            {
                File.WriteAllText(filePath, contents);
            }
            else if (!File.Exists(filePath))
            {
                File.Create(filePath).Dispose();
            }

            _ = project.ProjectItems.AddFromFile(filePath);

            if (open)
            {
                await OpenFileAsync(projectName, fileName, cancellationToken);
            }
        }

        private static string ConvertLanguageName(string languageName)
        {
            return languageName switch
            {
                LanguageNames.CSharp => "CSharp",
                LanguageNames.VisualBasic => "VisualBasic",
                LanguageNames.Razor => "CSharp",
                _ => throw new ArgumentException($"'{languageName}' is not supported.", nameof(languageName)),
            };
        }

        private async Task<string> GetAbsolutePathForProjectRelativeFilePathAsync(string projectName, string relativeFilePath, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var dte = await GetRequiredGlobalServiceAsync<SDTE, EnvDTE.DTE>(cancellationToken);
            var solution = dte.Solution;
            Assumes.Present(solution);

            var project = solution.Projects.Cast<EnvDTE.Project>().FirstOrDefault(x => x.Name == projectName);
            if(project is null)
            {
                Assert.True(false, $"{projectName} doesn't exist, had {string.Join(",", solution.Projects.Cast<EnvDTE.Project>().Select(p => p.Name))}");
            }
            Assert.NotNull(project);
            var projectPath = Path.GetDirectoryName(project.FullName);
            return Path.Combine(projectPath, relativeFilePath);
        }

        private async Task<bool> IsSolutionOpenAsync(CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var solution = await GetRequiredGlobalServiceAsync<SVsSolution, IVsSolution>(cancellationToken);
            ErrorHandler.ThrowOnFailure(solution.GetProperty((int)__VSPROPID.VSPROPID_IsSolutionOpen, out var isOpen));
            return (bool)isOpen;
        }

        /// <summary>
        /// Close the currently open solution without saving.
        /// </summary>
        public async Task CloseSolutionAsync(CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var solution = await GetRequiredGlobalServiceAsync<SVsSolution, IVsSolution>(cancellationToken);
            if (!await IsSolutionOpenAsync(cancellationToken))
            {
                return;
            }

            using var semaphore = new SemaphoreSlim(1);
            await using var solutionEvents = new SolutionEvents(JoinableTaskFactory, solution);

            await semaphore.WaitAsync(cancellationToken);

            void HandleAfterCloseSolution(object sender, EventArgs e)
                => semaphore.Release();

            solutionEvents.AfterCloseSolution += HandleAfterCloseSolution;
            try
            {
                ErrorHandler.ThrowOnFailure(solution.CloseSolutionElement((uint)__VSSLNCLOSEOPTIONS.SLNCLOSEOPT_DeleteProject | (uint)__VSSLNSAVEOPTIONS.SLNSAVEOPT_NoSave, null, 0));
                await semaphore.WaitAsync(cancellationToken);
            }
            finally
            {
                solutionEvents.AfterCloseSolution -= HandleAfterCloseSolution;
            }
        }

        private async Task<string> GetDirectoryNameAsync(CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var solution = await GetRequiredGlobalServiceAsync<SVsSolution, IVsSolution>(cancellationToken);
            ErrorHandler.ThrowOnFailure(solution.GetSolutionInfo(out _, out var solutionFileFullPath, out _));
            if (string.IsNullOrEmpty(solutionFileFullPath))
            {
                throw new InvalidOperationException();
            }

            return Path.GetDirectoryName(solutionFileFullPath);
        }

        private static string CreateTemporaryPath()
        {
            return Path.Combine(Path.GetTempPath(), "razor-test", Path.GetRandomFileName());
        }

        private async Task<EnvDTE.Project> GetProjectAsync(string nameOrFileName, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var dte = await GetRequiredGlobalServiceAsync<SDTE, EnvDTE.DTE>(cancellationToken);
            var solution = (EnvDTE80.Solution2)dte.Solution;
            return solution.Projects.OfType<EnvDTE.Project>().First(
                project =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    return string.Equals(project.FileName, nameOrFileName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(project.Name, nameOrFileName, StringComparison.OrdinalIgnoreCase);
                });
        }

        private sealed class SolutionEvents : IVsSolutionEvents, IAsyncDisposable
        {
            private readonly JoinableTaskFactory _joinableTaskFactory;
            private readonly IVsSolution _solution;
            private readonly uint _cookie;

            public SolutionEvents(JoinableTaskFactory joinableTaskFactory, IVsSolution solution)
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                _joinableTaskFactory = joinableTaskFactory;
                _solution = solution;
                ErrorHandler.ThrowOnFailure(solution.AdviseSolutionEvents(this, out _cookie));
            }

            public event EventHandler? AfterCloseSolution;

            public async ValueTask DisposeAsync()
            {
                await _joinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);
                ErrorHandler.ThrowOnFailure(_solution.UnadviseSolutionEvents(_cookie));
            }

            public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
            {
                return VSConstants.S_OK;
            }

            public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
            {
                return VSConstants.S_OK;
            }

            public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
            {
                return VSConstants.S_OK;
            }

            public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
            {
                return VSConstants.S_OK;
            }

            public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
            {
                return VSConstants.S_OK;
            }

            public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
            {
                return VSConstants.S_OK;
            }

            public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
            {
                return VSConstants.S_OK;
            }

            public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
            {
                return VSConstants.S_OK;
            }

            public int OnBeforeCloseSolution(object pUnkReserved)
            {
                return VSConstants.S_OK;
            }

            public int OnAfterCloseSolution(object pUnkReserved)
            {
                AfterCloseSolution?.Invoke(this, EventArgs.Empty);
                return VSConstants.S_OK;
            }
        }
    }
}
