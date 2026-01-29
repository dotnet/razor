// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudioCode.Razor.IntegrationTests.Infrastructure;
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudioCode.Razor.IntegrationTests.Scenarios;

/// <summary>
/// E2E tests for hover (Quick Info) in Razor files.
/// </summary>
public class HoverTests(ITestOutputHelper output) : VSCodeIntegrationTestBase(output)
{
    [Fact]
    public async Task Hover_CSharpField_ShowsFieldInfo()
    {
        // Arrange - Open Counter.razor from the dotnet new blazor template
        await OpenFileAsync("Components/Pages/Counter.razor");

        // Navigate to "currentCount" field in the @code block
        // Line 13: "    private int currentCount = 0;"
        // Column 17 is where "currentCount" starts (after "private int ")
        await Editor.GoToLineAsync(13, 17);

        // Act
        var hoverContent = await Razor.WaitForHoverContentAsync();

        // Assert
        Assert.NotNull(hoverContent);
        AssertEx.EqualOrDiff(
            "(field) int Counter.currentCount",
            hoverContent);
    }

    [Fact]
    public async Task Hover_Method_ShowsMethodSignature()
    {
        // Arrange - Open Counter.razor
        await OpenFileAsync("Components/Pages/Counter.razor");

        // Navigate to "IncrementCount" method in the @code block
        // Line 15: "    private void IncrementCount()"
        // Column 18 is where "IncrementCount" starts (after "private void ")
        await Editor.GoToLineAsync(15, 18);

        // Act
        var hoverContent = await Razor.WaitForHoverContentAsync();

        // Assert
        Assert.NotNull(hoverContent);
        AssertEx.EqualOrDiff(
            "void Counter.IncrementCount()",
            hoverContent);
    }

    [Fact]
    public async Task Hover_CSharpKeyword_ShowsTypeInfo()
    {
        // Arrange - Open Counter.razor
        await OpenFileAsync("Components/Pages/Counter.razor");

        // Navigate to "int" keyword in the @code block
        // Line 13: "    private int currentCount = 0;"
        // Column 13 is where "int" starts (after "private ")
        await Editor.GoToLineAsync(13, 13);

        // Act
        var hoverContent = await Razor.WaitForHoverContentAsync();

        // Assert
        Assert.NotNull(hoverContent);
        // int keyword should show System.Int32 info
        Assert.Contains("int", hoverContent);
        Assert.Contains("Int32", hoverContent);
    }
}
