// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;

namespace Microsoft.VisualStudio.Razor.IntegrationTests
{
    public class HoverTests : AbstractRazorEditorTest
    {
        [IdeFact]
        public async Task Hover_OverTagHelperElementAsync()
        {
            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.CounterRazorFile, ControlledHangMitigatingCancellationToken);

            await TestServices.Editor.PlaceCaretAsync("PageTitle", charsOffset: -1, ControlledHangMitigatingCancellationToken);

            var position = await TestServices.Editor.GetCaretPositionAsync(ControlledHangMitigatingCancellationToken);

            // Act
            var hoverResults = await TestServices.Editor.HoverAsync(position, ControlledHangMitigatingCancellationToken);

            // Assert
            var hoverResult = Assert.Single(hoverResults);
            var expectedResult = hoverResult;
            Assert.Equal(expectedResult, hoverResult);
        }
    }
}
