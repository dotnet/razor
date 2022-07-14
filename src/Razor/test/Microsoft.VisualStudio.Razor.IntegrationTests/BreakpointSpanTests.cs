﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;

namespace Microsoft.VisualStudio.Razor.IntegrationTests
{
    public class BreakpointSpanTests : AbstractRazorEditorTest
    {
        [IdeFact]
        public async Task SetBreakpoint_FirstCharacter_SpanAdjusts()
        {
            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.CounterRazorFile, ControlledHangMitigatingCancellationToken);

            // Wait for classifications to indicate Razor LSP is up and running
            await TestServices.Editor.WaitForComponentClassificationAsync(ControlledHangMitigatingCancellationToken);
            await TestServices.Editor.SetTextAsync("<p>@{ var abc = 123; }</p>", ControlledHangMitigatingCancellationToken);

            // Act
            await TestServices.Debugger.SetBreakpointAsync(RazorProjectConstants.CounterRazorFile, line: 1, character: 1, ControlledHangMitigatingCancellationToken);

            // Assert
            await TestServices.Debugger.VerifyBreakpointAsync(RazorProjectConstants.CounterRazorFile, line: 1, character: 7, ControlledHangMitigatingCancellationToken);
        }

        [IdeFact]
        public async Task SetBreakpoint_FirstCharacter_InvalidLine()
        {
            var version = await TestServices.Shell.GetVersionAsync(ControlledHangMitigatingCancellationToken);
            if (version < new System.Version(17, 3, 32412, 127))
            {
                // Functionality under test was added in v17.3-Preview1 (17.3.32412.127) so this test will
                // fail until CI is updated, so we'll skip it.
                //
                // Re-enabling this test is tracked by https://github.com/dotnet/razor-tooling/issues/6280
                return;
            }

            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.CounterRazorFile, ControlledHangMitigatingCancellationToken);

            // Wait for classifications to indicate Razor LSP is up and running
            await TestServices.Editor.WaitForComponentClassificationAsync(ControlledHangMitigatingCancellationToken);
            await TestServices.Editor.SetTextAsync(@"<p>@{
    var abc = 123;
}</p>", ControlledHangMitigatingCancellationToken);

            // Act
            var result = await TestServices.Debugger.SetBreakpointAsync(RazorProjectConstants.CounterRazorFile, line: 1, character: 1, ControlledHangMitigatingCancellationToken);

            // Assert
            Assert.False(result);
        }

        [IdeFact]
        public async Task SetBreakpoint_FirstCharacter_ValidLine()
        {
            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.CounterRazorFile, ControlledHangMitigatingCancellationToken);

            // Wait for classifications to indicate Razor LSP is up and running
            await TestServices.Editor.WaitForComponentClassificationAsync(ControlledHangMitigatingCancellationToken);
            await TestServices.Editor.SetTextAsync(@"<p>@{
    var abc = 123;
}</p>", ControlledHangMitigatingCancellationToken);

            // Act
            await TestServices.Debugger.SetBreakpointAsync(RazorProjectConstants.CounterRazorFile, line: 2, character: 1, ControlledHangMitigatingCancellationToken);

            // Assert
            await TestServices.Debugger.VerifyBreakpointAsync(RazorProjectConstants.CounterRazorFile, line: 2, character: 4, ControlledHangMitigatingCancellationToken);
        }
    }
}
