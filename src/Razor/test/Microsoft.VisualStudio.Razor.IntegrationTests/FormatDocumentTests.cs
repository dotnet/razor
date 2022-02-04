// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Xunit;

namespace Microsoft.VisualStudio.Razor.IntegrationTests
{
    public class FormatDocumentTests : AbstractRazorEditorTest
    {
        [IdeFact]
        public async Task FormatDocument_BasicCSharpFormatting()
        {
            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, CounterRazorFile, HangMitigatingCancellationToken);

            await TestServices.Editor.SetTextAsync(
@"@page ""/counter""

@code{
                                    private int currentCount = 0;

private void IncrementCount()
{
currentCount++;
}
}
", HangMitigatingCancellationToken);

            // Act
            await TestServices.Editor.InvokeFormatDocumentAsync(HangMitigatingCancellationToken);

            // Assert
            await TestServices.Editor.WaitForTextAsync(
@"@page ""/counter""

@code {
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }
}
", HangMitigatingCancellationToken);
        }
    }
}
