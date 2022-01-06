// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Razor.Integration.Test.InProcess;

namespace Microsoft.VisualStudio.Razor.Integration.Test
{
    public abstract class AbstractEditorTest : AbstractIntegrationTest
    {
        private readonly string? _solutionName;
        private readonly string? _projectName;
        private readonly string? _projectTemplate;

        protected AbstractEditorTest()
        {
        }

        protected AbstractEditorTest(string solutionName)
            : this(solutionName, WellKnownProjectTemplates.BlazorProject, "BlazorProject")
        {
        }

        protected AbstractEditorTest(string solutionName, string projectTemplate, string projectName)
        {
            _solutionName = solutionName;
            _projectTemplate = projectTemplate;
            _projectName = projectName;
        }

        protected abstract string LanguageName { get; }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            if (_solutionName is not null)
            {
                RazorDebug.AssertNotNull(_projectTemplate);
                RazorDebug.AssertNotNull(_projectName);

                await TestServices.SolutionExplorer.CreateSolutionAsync(_solutionName, HangMitigatingCancellationToken);
                await TestServices.SolutionExplorer.AddProjectAsync(_projectName, _projectTemplate, LanguageName, HangMitigatingCancellationToken);
                await TestServices.SolutionExplorer.RestoreNuGetPackagesAsync(ProjectName, HangMitigatingCancellationToken);

                await ClearEditorAsync(HangMitigatingCancellationToken);
            }
        }

        protected async Task ClearEditorAsync(CancellationToken cancellationToken)
            => await SetUpEditorAsync("$$", cancellationToken);

        protected async Task SetUpEditorAsync(string markupCode, CancellationToken cancellationToken)
        {
            MarkupTestFile.GetPosition(markupCode, out var code, out var caretPosition);

            await TestServices.Editor.DismissCompletionSessionsAsync(cancellationToken);
            await TestServices.Editor.DismissLightBulbSessionAsync(cancellationToken);

            await TestServices.Editor.SetTextAsync(code, cancellationToken);
            await TestServices.Editor.MoveCaretAsync(caretPosition, cancellationToken);
            await TestServices.Editor.ActivateAsync(cancellationToken);
        }
    }
}
