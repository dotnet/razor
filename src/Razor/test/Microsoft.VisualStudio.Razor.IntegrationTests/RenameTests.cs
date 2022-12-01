﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.IntegrationTests;

public class RenameTests : AbstractRazorEditorTest
{
    [IdeFact]
    public async Task Rename_ComponentAttribute_FromRazor()
    {
        // Open the file
        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.IndexRazorFile, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.PlaceCaretAsync("Title=", charsOffset: -1, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.WaitForComponentClassificationAsync(ControlledHangMitigatingCancellationToken);

        // Act
        await TestServices.Editor.InvokeRenameAsync(ControlledHangMitigatingCancellationToken);
        TestServices.Input.Send("ZooperDooper{ENTER}");

        // Assert
        // The rename operation causes SurveyPrompt.razor to be opened
        await TestServices.Editor.WaitForActiveWindowByFileAsync(RazorProjectConstants.SurveyPromptFile, ControlledHangMitigatingCancellationToken);
        await TestServices.Editor.VerifyTextContainsAsync("public string? ZooperDooper { get; set; }", ControlledHangMitigatingCancellationToken);
        await TestServices.Editor.VerifyTextContainsAsync("@ZooperDooper", ControlledHangMitigatingCancellationToken);

        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.IndexRazorFile, ControlledHangMitigatingCancellationToken);
        await TestServices.Editor.VerifyTextContainsAsync("<SurveyPrompt ZooperDooper=", ControlledHangMitigatingCancellationToken);
    }

    [IdeFact]
    public async Task Rename_ComponentAttribute_FromCSharpInRazor()
    {
        // Open the file
        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.SurveyPromptFile, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.PlaceCaretAsync("Title", charsOffset: 0, occurrence: 2, extendSelection: false, selectBlock: false, ControlledHangMitigatingCancellationToken);

        // Act
        await TestServices.Editor.InvokeRenameAsync(ControlledHangMitigatingCancellationToken);
        TestServices.Input.Send("ZooperDooper{ENTER}");

        // Assert
        // The rename operation causes Index.razor to be opened
        await TestServices.Editor.WaitForActiveWindowByFileAsync(RazorProjectConstants.IndexRazorFile, ControlledHangMitigatingCancellationToken);
        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.IndexRazorFile, ControlledHangMitigatingCancellationToken);
        await TestServices.Editor.VerifyTextContainsAsync("<SurveyPrompt ZooperDooper=", ControlledHangMitigatingCancellationToken);

        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.SurveyPromptFile, ControlledHangMitigatingCancellationToken);
        await TestServices.Editor.VerifyTextContainsAsync("public string? ZooperDooper { get; set; }", ControlledHangMitigatingCancellationToken);
        await TestServices.Editor.VerifyTextContainsAsync("@ZooperDooper", ControlledHangMitigatingCancellationToken);
    }

    [IdeFact]
    public async Task Rename_ComponentAttribute_FromCSharpInCSharp()
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

        await TestServices.Editor.PlaceCaretAsync("MyProperty", charsOffset: 0, occurrence: 2, extendSelection: false, selectBlock: false, ControlledHangMitigatingCancellationToken);

        // Act
        await TestServices.Editor.InvokeRenameAsync(ControlledHangMitigatingCancellationToken);
        TestServices.Input.Send("ZooperDooper{ENTER}");

        // Assert
        // The rename operation causes MyPage.razor to be opened
        await TestServices.Editor.WaitForActiveWindowByFileAsync("MyComponent.razor.cs", ControlledHangMitigatingCancellationToken);
        await TestServices.Editor.VerifyTextContainsAsync("public string? ZooperDooper { get; set; }", ControlledHangMitigatingCancellationToken);

        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, "MyPage.razor", ControlledHangMitigatingCancellationToken);
        await TestServices.Editor.VerifyTextContainsAsync("<MyComponent ZooperDooper=", ControlledHangMitigatingCancellationToken);

        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, "MyComponent.razor", ControlledHangMitigatingCancellationToken);
        await TestServices.Editor.VerifyTextContainsAsync("@ZooperDooper", ControlledHangMitigatingCancellationToken);
    }

    [IdeFact]
    public async Task Rename_ComponentAttribute_BoundAttribute()
    {
        // Create the files
        await TestServices.SolutionExplorer.AddFileAsync(RazorProjectConstants.BlazorProjectName,
            "MyComponent.razor",
            """
            <div></div>

            @code
            {
                [Parameter]
                public string? Value { get; set; }

                [Parameter]
                public EventCallback<string?> ValueChanged { get; set; }
            }
            """,
            cancellationToken: ControlledHangMitigatingCancellationToken);

        await TestServices.SolutionExplorer.AddFileAsync(RazorProjectConstants.BlazorProjectName,
            "MyPage.razor",
            """
            <MyComponent @bind-Value="value"></MyComponent>

            @code{
                string? value = "";
            }
            """,
            open: true,
            cancellationToken: ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.WaitForComponentClassificationAsync(ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.PlaceCaretAsync("Value=", charsOffset: -1, ControlledHangMitigatingCancellationToken);

        // Act
        await TestServices.Editor.InvokeRenameAsync(ControlledHangMitigatingCancellationToken);
        TestServices.Input.Send("ZooperDooper{ENTER}");

        // Assert
        // The rename operation causes MyPage.razor to be opened
        await TestServices.Editor.WaitForActiveWindowByFileAsync("MyComponent.razor", ControlledHangMitigatingCancellationToken);
        await TestServices.Editor.VerifyTextContainsAsync("public string? ZooperDooper { get; set; }", ControlledHangMitigatingCancellationToken);

        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, "MyPage.razor", ControlledHangMitigatingCancellationToken);
        await TestServices.Editor.VerifyTextContainsAsync("<MyComponent @bind-ZooperDooper=\"value\"></MyComponent>", ControlledHangMitigatingCancellationToken);
    }
}
