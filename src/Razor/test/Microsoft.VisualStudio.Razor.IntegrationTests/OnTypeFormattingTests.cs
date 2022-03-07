// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Xunit;

namespace Microsoft.VisualStudio.Razor.IntegrationTests
{
    public class OnTypeFormattingTests : AbstractRazorEditorTest
    {
        [IdeFact]
        public async Task TypeScript_Semicolon()
        {
            var version = await TestServices.Shell.GetVersionAsync(HangMitigatingCancellationToken);
            if (version < new System.Version(17, 2, 32302, 118))
            {
                return;
            }

            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, ErrorCshtmlFile, HangMitigatingCancellationToken);

            // Change text to refer back to Program class
            await TestServices.Editor.SetTextAsync(@"
<script>
    function F()
    {
        var    x     =   3
    }
</script>
", HangMitigatingCancellationToken);
            await TestServices.Editor.PlaceCaretAsync("3", charsOffset: 1, HangMitigatingCancellationToken);

            // Act
            TestServices.Input.Send(";");

            // Assert
            await TestServices.Editor.WaitForCurrentLineTextAsync("var x = 3;", HangMitigatingCancellationToken);
        }
    }
}
