// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.IntegrationTests;

public class FindAllReferencesTests : AbstractRazorEditorTest
{
    [IdeFact]
    public async Task FindAllReferences_CSharpInRazor()
    {
        // Open the file
        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.CounterRazorFile, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.PlaceCaretAsync("IncrementCount", charsOffset: -1, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.WaitForComponentClassificationAsync(ControlledHangMitigatingCancellationToken);

        // Act
        await TestServices.Editor.InvokeFindAllReferencesAsync(ControlledHangMitigatingCancellationToken);

        // Assert
        var results = await TestServices.FindReferencesWindow.WaitForContentsAsync(ControlledHangMitigatingCancellationToken, expected: 2);

        Assert.Collection(
            results,
            new Action<ITableEntryHandle2>[]
            {
                reference =>
                {
                    Assert.Equal(expected: "private void IncrementCount()", actual: reference.TryGetValue(StandardTableKeyNames.Text, out string code) ? code : null);
                    Assert.True(reference.TryGetValue(StandardTableKeyNames.DocumentName, out string documentName));
                    Assert.Equal(expected: "Counter.razor", Path.GetFileName(documentName));
                },
                reference =>
                {
                    Assert.Equal(expected: "IncrementCount", actual: reference.TryGetValue(StandardTableKeyNames.Text, out string code) ? code : null);
                    Assert.True(reference.TryGetValue(StandardTableKeyNames.DocumentName, out string documentName));
                    Assert.Equal(expected: "Counter.razor", Path.GetFileName(documentName));
                }
            });
    }

    [IdeFact(Skip = "Will be enabled by a one line change after https://github.com/dotnet/razor/issues/6655 goes in")]
    public async Task FindAllReferences_ComponentAttribute_FromRazor()
    {
        // Open the file
        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.IndexRazorFile, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.PlaceCaretAsync("Title=", charsOffset: -1, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.WaitForComponentClassificationAsync(ControlledHangMitigatingCancellationToken);

        // Act
        await TestServices.Editor.InvokeFindAllReferencesAsync(ControlledHangMitigatingCancellationToken);

        // Assert
        var results = await TestServices.FindReferencesWindow.WaitForContentsAsync(ControlledHangMitigatingCancellationToken, expected: 3);

        Assert.Collection(
            results,
            new Action<ITableEntryHandle2>[]
            {
                reference =>
                {
                    Assert.Equal(expected: "public string? Title { get; set; }", actual: reference.TryGetValue(StandardTableKeyNames.Text, out string code) ? code : null);
                    Assert.True(reference.TryGetValue(StandardTableKeyNames.DocumentName, out string documentName));
                    Assert.Equal(expected: "SurveyPrompt.razor", Path.GetFileName(documentName));
                },
                reference =>
                {
                    Assert.Equal(expected: "Title", actual: reference.TryGetValue(StandardTableKeyNames.Text, out string code) ? code : null);
                    Assert.True(reference.TryGetValue(StandardTableKeyNames.DocumentName, out string documentName));
                    Assert.Equal(expected: "Index.razor", Path.GetFileName(documentName));
                },
                reference =>
                {
                    Assert.Equal(expected: "Title", actual: reference.TryGetValue(StandardTableKeyNames.Text, out string code) ? code : null);
                    Assert.True(reference.TryGetValue(StandardTableKeyNames.DocumentName, out string documentName));
                    Assert.Equal(expected: "SurveyPrompt.razor", Path.GetFileName(documentName));
                }
            });
    }

    [IdeFact]
    public async Task FindAllReferences_ComponentAttribute_FromCSharpInRazor()
    {
        // Open the file
        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.SurveyPromptFile, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.PlaceCaretAsync("Title", charsOffset: 0, occurrence: 2, extendSelection: false, selectBlock: false, ControlledHangMitigatingCancellationToken);

        // This is annoying, but if we do the FAR too quickly, we just get results from the current file
        await Task.Delay(500);

        // Act
        await TestServices.Editor.InvokeFindAllReferencesAsync(ControlledHangMitigatingCancellationToken);

        // Assert
        var results = await TestServices.FindReferencesWindow.WaitForContentsAsync(ControlledHangMitigatingCancellationToken, expected: 3);

        Assert.Collection(
            results,
            new Action<ITableEntryHandle2>[]
            {
                reference =>
                {
                    Assert.Equal(expected: "public string? Title { get; set; }", actual: reference.TryGetValue(StandardTableKeyNames.Text, out string code) ? code : null);
                    Assert.True(reference.TryGetValue(StandardTableKeyNames.DocumentName, out string documentName));
                    Assert.Equal(expected: "SurveyPrompt.razor", Path.GetFileName(documentName));
                },
                reference =>
                {
                    Assert.Equal(expected: "Title", actual: reference.TryGetValue(StandardTableKeyNames.Text, out string code) ? code : null);
                    Assert.True(reference.TryGetValue(StandardTableKeyNames.DocumentName, out string documentName));
                    Assert.Equal(expected: "Index.razor", Path.GetFileName(documentName));
                },
                reference =>
                {
                    Assert.Equal(expected: "Title", actual: reference.TryGetValue(StandardTableKeyNames.Text, out string code) ? code : null);
                    Assert.True(reference.TryGetValue(StandardTableKeyNames.DocumentName, out string documentName));
                    Assert.Equal(expected: "SurveyPrompt.razor", Path.GetFileName(documentName));
                }
            });
    }

    [IdeFact]
    public async Task FindAllReferences_ComponentAttribute_FromCSharpInCSharp()
    {
        // Create the file
        await TestServices.SolutionExplorer.AddFileAsync(RazorProjectConstants.BlazorProjectName,
            "MyComponent.razor",
            """
                @MyProperty
                """,
            open: false,
            cancellationToken: ControlledHangMitigatingCancellationToken);

        await TestServices.SolutionExplorer.AddFileAsync(RazorProjectConstants.BlazorProjectName,
            "MyComponent.razor.cs",
            """
                namespace BlazorProject;

                public partial class MyComponent
                {
                    [Microsoft.AspNetCore.Components.ParameterAttribute]
                    public string? MyProperty { get; set; }
                }
            
                """,
            open: false,
            cancellationToken: ControlledHangMitigatingCancellationToken);

        await TestServices.SolutionExplorer.AddFileAsync(RazorProjectConstants.BlazorProjectName,
            "MyPage.razor",
            """
                <MyComponent MyProperty="123" />
                """,
            open: true,
            cancellationToken: ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.WaitForComponentClassificationAsync(ControlledHangMitigatingCancellationToken);

        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, "MyComponent.razor.cs", ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.PlaceCaretAsync("MyProperty", charsOffset: -1, ControlledHangMitigatingCancellationToken);

        // This is annoying, but if we do the FAR too quickly, we just get one result from the current file
        await Task.Delay(500);

        // Act
        await TestServices.Editor.InvokeFindAllReferencesAsync(ControlledHangMitigatingCancellationToken);

        // Assert
        var results = await TestServices.FindReferencesWindow.WaitForContentsAsync(ControlledHangMitigatingCancellationToken, expected: 3);

        Assert.Collection(
            results,
            new Action<ITableEntryHandle2>[]
            {
                reference =>
                {
                    Assert.Equal(expected: "public string? MyProperty { get; set; }", actual: reference.TryGetValue(StandardTableKeyNames.Text, out string code) ? code : null);
                    Assert.True(reference.TryGetValue(StandardTableKeyNames.DocumentName, out string documentName));
                    Assert.Equal(expected: "MyComponent.razor.cs", Path.GetFileName(documentName));
                },
                reference =>
                {
                    Assert.Equal(expected: "@MyProperty", actual: reference.TryGetValue(StandardTableKeyNames.Text, out string code) ? code : null);
                    Assert.True(reference.TryGetValue(StandardTableKeyNames.DocumentName, out string documentName));
                    Assert.Equal(expected: "MyComponent.razor", Path.GetFileName(documentName));
                },
                reference =>
                {
                    Assert.Equal(expected: "<MyComponent MyProperty=\"123\" />", actual: reference.TryGetValue(StandardTableKeyNames.Text, out string code) ? code : null);
                    Assert.True(reference.TryGetValue(StandardTableKeyNames.DocumentName, out string documentName));
                    Assert.Equal(expected: "MyPage.razor", Path.GetFileName(documentName));
                }
            });
    }
}
