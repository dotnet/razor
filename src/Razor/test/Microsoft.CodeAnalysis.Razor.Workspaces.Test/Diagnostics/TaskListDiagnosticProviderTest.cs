﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.Razor.Workspaces.Test.Diagnostics;

public class TaskListDiagnosticProviderTest(ITestOutputHelper testOutputHelper) : ToolingTestBase(testOutputHelper)
{
    [Fact]
    public void TODO()
    {
        VerifyTODOComments("""
            <div>
                @*[| TODO: This is it |]*@
            </div>
            """);
    }

    [Fact]
    public void TODO_Multiline()
    {
        VerifyTODOComments("""
            <div>
                @*[|
                    TODO: This is it
                |]*@
            </div>
            """);
    }

    [Fact]
    public void TODOnt()
    {
        VerifyTODOComments("""
            <div>
                @* TODONT: This is it *@
            </div>
            """);
    }

    [Fact]
    public void DoesntFit()
    {
        VerifyTODOComments("""
            <div>
            </div>

            @* Real *@
            """);
    }

    private static void VerifyTODOComments(TestCode input)
    {
        var codeDocument = TestRazorCodeDocument.Create(input.Text);
        codeDocument.SetSyntaxTree(RazorSyntaxTree.Parse(codeDocument.Source));
        var inputText = codeDocument.Source.Text;

        var diagnostics = TaskListDiagnosticProvider.GetTaskListDiagnostics(codeDocument, ["TODO", "ReallyLongPrefix"]);

        var markers = diagnostics.SelectMany(d =>
            new[] {
                (index: inputText.GetTextSpan(d.Range.ToLinePositionSpan()).Start, text: "[|"),
                (index: inputText.GetTextSpan(d.Range.ToLinePositionSpan()).End, text:"|]")
            });

        var testOutput = input.Text;
        foreach (var (index, text) in markers.OrderByDescending(i => i.index))
        {
            testOutput = testOutput.Insert(index, text);
        }

        AssertEx.EqualOrDiff(input.OriginalInput, testOutput);
    }
}
