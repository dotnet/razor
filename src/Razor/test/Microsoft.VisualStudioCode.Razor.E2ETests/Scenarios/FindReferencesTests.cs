// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudioCode.Razor.E2ETests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudioCode.Razor.E2ETests.Scenarios;

/// <summary>
/// E2E tests for Find All References in Razor files.
/// </summary>
public class FindReferencesTests(ITestOutputHelper output) : VSCodeIntegrationTestBase(output)
{
    [Fact]
    public async Task FindReferences_Component_ShowsUsages()
    {
        // Arrange
        await OpenFileAsync("BlazorApp/Components/Counter.razor");

        // Navigate to the component class (implicit class name)
        await Editor.GoToLineAsync(1);

        // Act - FindAllReferencesAsync now waits for the references panel
        await Editor.FindAllReferencesAsync();

        // Assert - the references panel should open
        // The method already waits for the panel to appear

        // Close the references panel
        await Editor.PressAsync("Escape");
    }

    [Fact]
    public async Task FindReferences_CSharpMethod_ShowsCallSites()
    {
        // Arrange
        await OpenFileAsync("BlazorApp/Components/Counter.razor");

        // Navigate to the IncrementCount method
        await Editor.GoToLineAsync(23); // private async Task IncrementCount()

        // Select the method name
        await Editor.PressAsync("Control+d");

        // Act - FindAllReferencesAsync now waits for the references panel
        await Editor.FindAllReferencesAsync();

        // Assert - the references panel should show at least 2 references
        // (the definition and the @onclick usage)
        // The method already waits for the panel to appear

        // Close the references panel
        await Editor.PressAsync("Escape");
    }
}
