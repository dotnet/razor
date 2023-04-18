// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;
using Xunit;

namespace Microsoft.VisualStudio.Razor.IntegrationTests;

public class OnTypeFormattingTests : AbstractRazorEditorTest
{
    [IdeFact]
    public async Task TypeScript_Semicolon()
    {
        // Open the file
        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.ErrorCshtmlFile, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.WaitForSemanticClassificationAsync("RazorTagHelperElement", ControlledHangMitigatingCancellationToken, count: 2);

        // Change text to refer back to Program class
        await TestServices.Editor.SetTextAsync(@"
<script>
    function F()
    {
        var    x     =   3
    }
</script>
", ControlledHangMitigatingCancellationToken);

        // We need to wait for the TypeScript LSP server to be ready, but it doesn't support semantic classifications,
        // which is our normal method, so we use hover instead.
        await TestServices.Editor.PlaceCaretAsync("F()", charsOffset: -1, ControlledHangMitigatingCancellationToken);
        var position = await TestServices.Editor.GetCaretPositionAsync(ControlledHangMitigatingCancellationToken);
        await TestServices.Editor.GetHoverStringAsync(position, ControlledHangMitigatingCancellationToken);

        // Now back to your regularly scheduled on type formatting test
        await TestServices.Editor.PlaceCaretAsync("3", charsOffset: 1, ControlledHangMitigatingCancellationToken);

        // Act
        TestServices.Input.Send(";");

        // Assert
        await TestServices.Editor.WaitForCurrentLineTextAsync("var x = 3;", ControlledHangMitigatingCancellationToken);
    }
}
