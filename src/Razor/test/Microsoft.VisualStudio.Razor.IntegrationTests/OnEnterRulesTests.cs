// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Xunit;

namespace Microsoft.VisualStudio.Razor.IntegrationTests;

public class OnEnterRulesTests : AbstractRazorEditorTest
{
    [IdeFact]
    public async Task OnEnterRules_BetweenStartAndEnd()
    {
        // Arrange
        await PrepareDocumentAsync(@"
<button class='classifier'></button>
");
        // Act
        await TestServices.Editor.PlaceCaretAsync(">", charsOffset: 1, HangMitigatingCancellationToken);
        TestServices.Input.Send("{ENTER}");
        TestServices.Input.Send("A");

        // Assert
        await TestServices.Editor.VerifyTextContainsAsync(@"
<button class='classifier'>
    A
</button>
", HangMitigatingCancellationToken);
    }

    [IdeFact]
    public async Task OnEnterRules_AtEndOfTag()
    {
        // Arrange
        await PrepareDocumentAsync(@"
<button stuff></button>
");

        // Act
        await TestServices.Editor.PlaceCaretAsync("</button>", charsOffset: 1, HangMitigatingCancellationToken);
        TestServices.Input.Send("{ENTER}");
        TestServices.Input.Send("A");

        // Assert
        await TestServices.Editor.VerifyTextContainsAsync(@"
<button stuff></button>
A
", HangMitigatingCancellationToken);
    }

    [IdeFact]
    public async Task OnEnterRules_BeforeAttribute()
    {
        // Arrange
        await PrepareDocumentAsync(@"
<button class='thing' stuff></button>
");

        // Act
        await TestServices.Editor.PlaceCaretAsync("button", charsOffset: 1, HangMitigatingCancellationToken);

        TestServices.Input.Send("{ENTER}");

        // Assert
        await TestServices.Editor.VerifyTextContainsAsync(@"
<button
    class='thing' stuff></button>
", HangMitigatingCancellationToken);
    }

    [IdeFact]
    public async Task OnEnterRules_EmptyAttribute()
    {
        // Arrange
        await PrepareDocumentAsync(@"
<button class='someclass' @onclick='thing' stuff ></button>
");

        // Act
        await TestServices.Editor.PlaceCaretAsync("stuff", charsOffset: 1, HangMitigatingCancellationToken);
        TestServices.Input.Send("{ENTER}");

        // Assert
        await TestServices.Editor.VerifyTextContainsAsync(@"
<button class='someclass' @onclick='thing' stuff
    ></button>
", HangMitigatingCancellationToken);
    }

    [IdeFact]
    public async Task OnEnterRules_DoubleQuote()
    {
        // Arrange
        await PrepareDocumentAsync(@"
<button class=""someclass"" @onclick='thing' stuff ></button>
");

        // Act
        await TestServices.Editor.PlaceCaretAsync("button", charsOffset: 1, HangMitigatingCancellationToken);
        TestServices.Input.Send("{ENTER}");

        // Assert
        await TestServices.Editor.VerifyTextContainsAsync(@"
<button
    class=""someclass"" @onclick='thing' stuff ></button>
", HangMitigatingCancellationToken);
    }

    [IdeFact]
    public async Task OnEnterRules_DirectiveAttribute()
    {
        // Arrange
        await PrepareDocumentAsync(@"
<button class='someclass' @onclick='thing' stuff ></button>
");

        // Act
        await TestServices.Editor.PlaceCaretAsync("@onclick='thing'", charsOffset: 1, HangMitigatingCancellationToken);
        TestServices.Input.Send("{ENTER}");

        // Assert
        await TestServices.Editor.VerifyTextContainsAsync(@"
<button class='someclass' @onclick='thing'
    stuff ></button>
", HangMitigatingCancellationToken);
    }

    [IdeFact]
    public async Task OnEnterRules_UnfinishedTag()
    {
        // Arrange
        await PrepareDocumentAsync(@"
<button class='thing' stuff
");

        // Act
        await TestServices.Editor.PlaceCaretAsync("button", charsOffset: 1, HangMitigatingCancellationToken);
        TestServices.Input.Send("{ENTER}");

        // Assert
        await TestServices.Editor.VerifyTextContainsAsync(@"
<button
    class='thing' stuff
", HangMitigatingCancellationToken);
    }

    private async Task PrepareDocumentAsync(string content)
    {
        // Arrange
        await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, CounterRazorFile, HangMitigatingCancellationToken);
        await TestServices.Editor.SetTextAsync(content, HangMitigatingCancellationToken);
    }
}

