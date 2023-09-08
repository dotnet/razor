﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.IntegrationTests;

public class DiagnosticTests(ITestOutputHelper testOutputHelper) : AbstractRazorEditorTest(testOutputHelper)
{
    [ConditionalSkipIdeFact(Issue = "https://github.com/dotnet/razor/issues/8150")]
    public async Task Diagnostics_ShowErrors_Razor()
    {
        // Arrange
        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.CounterRazorFile, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.SetTextAsync(@"
<h1>
<PageTitle>

@code{
    public void Function(){
        return """"
    }
}
", ControlledHangMitigatingCancellationToken);

        // Act
        var errors = await TestServices.ErrorList.WaitForErrorsAsync("Counter.razor", expectedCount: 3, ControlledHangMitigatingCancellationToken);

        // Assert
        Assert.Collection(errors,
            (error) =>
            {
                Assert.Equal("Counter.razor(2, 1): error RZ9980: Unclosed tag 'h1' with no matching end tag.", error);
            },
            (error) =>
            {
                Assert.Equal("Counter.razor(3, 2): error RZ1034: Found a malformed 'PageTitle' tag helper. Tag helpers must have a start and end tag or be self closing.", error);
            },
            (error) =>
            {
                Assert.Equal("Counter.razor(7, 18): error CS1002: ; expected", error);
            },
            (error) =>
            {
                Assert.Equal("Counter.razor(7, 9): error CS0127: Since 'Counter.Function()' returns void, a return keyword must not be followed by an object expression", error);
            });
    }

    [ConditionalSkipIdeFact(Issue = "https://github.com/dotnet/razor/issues/8150")]
    public async Task Diagnostics_ShowErrors_Html()
    {
        // Arrange
        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.ErrorCshtmlFile, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.SetTextAsync(@"
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers

<!DOCTYPE html>
<html lang=""en"">
<head>
</head>

<body>
    <p
</body>
</html>

", ControlledHangMitigatingCancellationToken);

        // Act
        var errors = await TestServices.ErrorList.WaitForErrorsAsync("Error.cshtml", expectedCount: 1, ControlledHangMitigatingCancellationToken);

        // Assert
        Assert.Collection(errors,
            (error) =>
            {
                Assert.Equal("Error.cshtml(10, 6): warning HTML0001: Element start tag is missing closing angle bracket.", error);
            });
    }

    [ConditionalSkipIdeFact(Issue = "https://github.com/dotnet/razor/issues/8150")]
    public async Task Diagnostics_ShowErrors_CSharp()
    {
        // Arrange
        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.ErrorCshtmlFile, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.SetTextAsync(@"
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers

<!DOCTYPE html>
<html lang=""en"">
<head>
</head>

<body>
    <input asp-for=""Test"" />
</body>
</html>

", ControlledHangMitigatingCancellationToken);

        // Act
        var errors = await TestServices.ErrorList.WaitForErrorsAsync("Error.cshtml", expectedCount: 1, ControlledHangMitigatingCancellationToken);

        // Assert
        Assert.Collection(errors,
            (error) =>
            {
                Assert.Equal("Error.cshtml(10, 21): error CS1963: An expression tree may not contain a dynamic operation", error);
            });
    }

    [ConditionalSkipIdeFact(Issue = "https://github.com/dotnet/razor/issues/8150")]
    public async Task Diagnostics_ShowErrors_CSharp_NoDocType()
    {
        // Why this test, when we have the above test, and they seem so similar, and we also have Diagnostics_ShowErrors_CSharpAndHtml you ask? Well I'll tell you!
        //
        // In the above test, with a doctype, the Html LSP server returns a diagnostic result containing one item, which has an empty array of actual diagnostics within it.
        // In Diagnostics_ShowErrors_CSharpAndHtml, the Html LSP server returns a diagnostic result containing one item, which has one actual diagnostic inside it.
        // In this test, with no doctype, the Html LSP server returns a diagnostic result containing one item, which has null for the actual diagnostics within it.
        //
        // The Visual Studio error system (squiggles and error list) didn't like that last case, so given that quirkiness and the slight differences in results, there
        // is value in having a range of tests to make sure nothing regresses.

        // Arrange
        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.ErrorCshtmlFile, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.SetTextAsync(@"
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers

<html lang=""en"">
<head>
</head>

<body>
    <input asp-for=""Test"" />
</body>
</html>

", ControlledHangMitigatingCancellationToken);

        // Act
        var errors = await TestServices.ErrorList.WaitForErrorsAsync("Error.cshtml", expectedCount: 1, ControlledHangMitigatingCancellationToken);

        // Assert
        Assert.Collection(errors,
            (error) =>
            {
                Assert.Equal("Error.cshtml(9, 21): error CS1963: An expression tree may not contain a dynamic operation", error);
            });
    }

    [ConditionalSkipIdeFact(Issue = "https://github.com/dotnet/razor/issues/8150")]
    public async Task Diagnostics_ShowErrors_CSharpAndHtml()
    {
        // Arrange
        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.ErrorCshtmlFile, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.SetTextAsync(@"
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers

<!DOCTYPE html>
<html lang=""en"">
<head>
</head>

<body>
    <input asp-for=""Test"" />
    <li>
</body>
</html>

", ControlledHangMitigatingCancellationToken);

        // Act
        var errors = await TestServices.ErrorList.WaitForErrorsAsync("Error.cshtml", expectedCount: 2, ControlledHangMitigatingCancellationToken);

        // Assert
        Assert.Collection(errors,
            (error) =>
            {
                Assert.Equal("Error.cshtml(10, 21): error CS1963: An expression tree may not contain a dynamic operation", error);
            },
            (error) =>
            {
                Assert.Equal("Error.cshtml(11, 6): warning HTML0204: Element 'li' cannot be nested inside element 'body'.", error);
            });
    }
}
