// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;

namespace Microsoft.VisualStudio.Razor.Integration.Test
{
    public class BreakpointSpanTests : AbstractRazorEditorTest
    {
        [IdeFact]
        public async Task SetBreakpoint_FirstCharacter_SpanAdjusts()
        {
            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, CounterRazorFile, HangMitigatingCancellationToken);

            // Wait for classifications to indicate Razor LSP is up and running
            await TestServices.Editor.WaitForClassificationAsync(HangMitigatingCancellationToken);
            await TestServices.Editor.SetTextAsync("<p>@{ var abc = 123; }</p>", HangMitigatingCancellationToken);

            // Act
            await TestServices.Debugger.SetBreakpointAsync(CounterRazorFile, line: 1, character: 1, HangMitigatingCancellationToken);

            // Assert
            await TestServices.Debugger.VerifyBreakpointAsync(CounterRazorFile, line: 1, character: 7, HangMitigatingCancellationToken);
        }

        [IdeFact]
        public async Task SetBreakpoint_FirstCharacter_InvalidLine()
        {
            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, CounterRazorFile, HangMitigatingCancellationToken);

            // Wait for classifications to indicate Razor LSP is up and running
            await TestServices.Editor.WaitForClassificationAsync(HangMitigatingCancellationToken);
            await TestServices.Editor.SetTextAsync(@"<p>@{
    var abc = 123;
}</p>", HangMitigatingCancellationToken);

            // Act
            var result = await TestServices.Debugger.SetBreakpointAsync(CounterRazorFile, line: 1, character: 1, HangMitigatingCancellationToken);

            // Assert
            Assert.False(result);
        }

        [IdeFact]
        public async Task SetBreakpoint_FirstCharacter_ValidLine()
        {
            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, CounterRazorFile, HangMitigatingCancellationToken);

            // Wait for classifications to indicate Razor LSP is up and running
            await TestServices.Editor.WaitForClassificationAsync(HangMitigatingCancellationToken);
            await TestServices.Editor.SetTextAsync(@"<p>@{
    var abc = 123;
}</p>", HangMitigatingCancellationToken);

            // Act
            await TestServices.Debugger.SetBreakpointAsync(CounterRazorFile, line: 2, character: 1, HangMitigatingCancellationToken);

            // Assert
            await TestServices.Debugger.VerifyBreakpointAsync(CounterRazorFile, line: 2, character: 4, HangMitigatingCancellationToken);
        }
    }
}
