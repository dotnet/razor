// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor.CohostingShared;
using Microsoft.CodeAnalysis.Rename;
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public class CohostRoslynRenameTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Fact]
    public Task CSharp_Method()
        => VerifyRenamesAsync(
            csharpFile: """
                public class MyClass
                {
                    public string MyMethod()
                    {
                        return $"Hi from {nameof(MyMethod)}";
                    }
                }
                """,
            razorFile: """
                This is a Razor document.

                <h1>@_myClass.MyMethod()</h1>

                @code
                {
                    private MyClass _myClass = new MyClass();

                    public string M()
                    {
                        return _myClass.MyMethod();
                    }
                }

                The end.
                """,
            symbolToRename: "MyMethod",
            newName: "CallThisFunction",
            expectedCSharpFile: """
                public class MyClass
                {
                    public string CallThisFunction()
                    {
                        return $"Hi from {nameof(CallThisFunction)}";
                    }
                }
                """,
            expectedRazorFile: """
                This is a Razor document.
                
                <h1>@_myClass.CallThisFunction()</h1>
                
                @code
                {
                    private MyClass _myClass = new MyClass();
                
                    public string M()
                    {
                        return _myClass.CallThisFunction();
                    }
                }
                
                The end.
                """);

    private async Task VerifyRenamesAsync(
        string csharpFile,
        string razorFile,
        string symbolToRename,
        string newName,
        string expectedCSharpFile,
        string expectedRazorFile)
    {
        var razorDocument = CreateProjectAndRazorDocument(razorFile, additionalFiles: [("File.cs", csharpFile)]);
        var project = razorDocument.Project;
        var csharpDocument = project.Documents.First();

        var compilation = await project.GetCompilationAsync(DisposalToken);

        var symbol = compilation.AssumeNotNull().GetSymbolsWithName(symbolToRename).First();

        var solution = await Renamer.RenameSymbolAsync(project.Solution, symbol, new SymbolRenameOptions(), newName, DisposalToken);

        Assert.NotSame(project.Solution, solution);

        // Make sure the rename worked in the C# document
        var csharpDocumentAfterRename = solution.GetDocument(csharpDocument.Id).AssumeNotNull();
        var csharpText = await csharpDocumentAfterRename.GetTextAsync(DisposalToken);
        AssertEx.EqualOrDiff(expectedCSharpFile, csharpText.ToString());

        // Normally in VS, TryApplyChanges would be called, and that calls into our edit mapping service.
        Assert.True(razorDocument.TryComputeHintNameFromRazorDocument(out var hintName));
        var generatedDoc = await project.TryGetSourceGeneratedDocumentFromHintNameAsync(hintName, DisposalToken);
        Assert.NotNull(generatedDoc);
        var renamedGeneratedDoc = await solution.GetRequiredProject(project.Id).TryGetSourceGeneratedDocumentFromHintNameAsync(hintName, DisposalToken);
        Assert.NotNull(renamedGeneratedDoc);

        // It could be argued this class is really a RazorSourceGeneratedDocumentSpanMappingService test :)
        var mappingService = new RazorSourceGeneratedDocumentSpanMappingService(RemoteServiceInvoker);
        var changes = await mappingService.GetMappedTextChangesAsync(generatedDoc, renamedGeneratedDoc, DisposalToken);

        var razorDocumentAfterRename = solution.GetAdditionalDocument(razorDocument.Id).AssumeNotNull();
        var razorText = await razorDocumentAfterRename.GetTextAsync(DisposalToken);

        foreach (var change in changes)
        {
            Assert.Equal(razorDocument.FilePath, change.FilePath);
            razorText = razorText.WithChanges(change.TextChanges);
        }

        AssertEx.EqualOrDiff(expectedRazorFile, razorText.ToString());
    }
}
