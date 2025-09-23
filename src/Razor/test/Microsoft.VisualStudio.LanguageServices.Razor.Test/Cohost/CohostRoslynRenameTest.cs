﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.Mef;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost.Handlers;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.CohostingShared;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public class CohostRoslynRenameTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Theory]
    [CombinatorialData]
    public Task CSharp_Method(bool useLsp)
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

                <h1>@_myClass.MyM$$ethod()</h1>

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
                """,
            useLsp);

    [Theory]
    [CombinatorialData]
    public Task CSharp_Local(bool useLsp)
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

                @{var someV$$ariable = true;} @someVariable

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
           newName: "fishPants",
           expectedCSharpFile: """
                public class MyClass
                {
                    public string MyMethod()
                    {
                        return $"Hi from {nameof(MyMethod)}";
                    }
                }
                """,
           expectedRazorFile: """
                This is a Razor document.
                
                @{var fishPants = true;} @fishPants
                
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
           useLsp);

    private protected override TestComposition ConfigureRoslynDevenvComposition(TestComposition composition)
    {
        return composition
            .AddParts(typeof(RazorSourceGeneratedDocumentSpanMappingService))
            .AddParts(typeof(ExportableRemoteServiceInvoker));
    }

    private async Task VerifyRenamesAsync(
        string csharpFile,
        TestCode razorFile,
        string newName,
        string expectedCSharpFile,
        string expectedRazorFile,
        bool useLsp)
    {
        var razorDocument = CreateProjectAndRazorDocument(razorFile.Text, additionalFiles: [(Path.Combine(TestProjectData.SomeProjectPath, "File.cs"), csharpFile)]);
        var project = razorDocument.Project;
        var csharpDocument = project.Documents.First();

        var compilation = await project.GetCompilationAsync(DisposalToken);

        Assert.True(razorDocument.TryComputeHintNameFromRazorDocument(out var hintName));
        var generatedDocument = await project.TryGetSourceGeneratedDocumentFromHintNameAsync(hintName, DisposalToken);

        var node = await GetSyntaxNodeAsync(generatedDocument.AssumeNotNull(), razorFile.Position, razorDocument);

        if (useLsp)
        {
            await VerifyLspRenameAsync(newName, expectedCSharpFile, expectedRazorFile, razorDocument, csharpDocument, node);
        }
        else
        {
            var symbol = FindSymbolToRename(compilation.AssumeNotNull(), node);
            await VerifyVSRenameAsync(newName, expectedCSharpFile, expectedRazorFile, razorDocument, project, csharpDocument, symbol);
        }
    }

    private ISymbol FindSymbolToRename(Compilation compilation, SyntaxNode node)
    {
        var semanticModel = compilation.GetSemanticModel(node.SyntaxTree);
        var symbol = semanticModel.GetDeclaredSymbol(node, DisposalToken);
        if (symbol is null)
        {
            symbol = semanticModel.GetSymbolInfo(node, DisposalToken).Symbol;
        }

        Assert.NotNull(symbol);
        return symbol;
    }

    private async Task<SyntaxNode> GetSyntaxNodeAsync(Document document, int position, TextDocument razorDocument)
    {
        var snapshotManager = OOPExportProvider.GetExportedValue<RemoteSnapshotManager>();
        var documentMappingService = OOPExportProvider.GetExportedValue<IDocumentMappingService>();
        var snapshot = snapshotManager.GetSnapshot(razorDocument);
        var codeDocument = await snapshot.GetGeneratedOutputAsync(DisposalToken);

        Assert.True(documentMappingService.TryMapToCSharpDocumentPosition(codeDocument.GetCSharpDocument().AssumeNotNull(), position, out var csharpPosition, out _));

        var sourceText = await document.GetTextAsync(DisposalToken);
        var span = sourceText.GetTextSpan(csharpPosition, csharpPosition);
        var tree = await document.AssumeNotNull().GetSyntaxTreeAsync(DisposalToken);
        var root = await tree.AssumeNotNull().GetRootAsync(DisposalToken);

        return root.FindNode(span);
    }

    private async Task VerifyVSRenameAsync(string newName, string expectedCSharpFile, string expectedRazorFile, TextDocument razorDocument, Project project, Document csharpDocument, ISymbol symbol)
    {
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

    private async Task VerifyLspRenameAsync(string newName, string expectedCSharpFile, string expectedRazorFile, TextDocument razorDocument, Document csharpDocument, SyntaxNode node)
    {
        // Normally in cohosting tests we directly construct and invoke the endpoints, but in this scenario Roslyn is going to do it
        // using a service in their MEF composition, so we have to jump through an extra hook to hook up our test invoker.
        var invoker = RoslynDevenvExportProvider.AssumeNotNull().GetExportedValue<ExportableRemoteServiceInvoker>();
        invoker.SetInvoker(RemoteServiceInvoker);

        var tree = node.SyntaxTree;
        var documentToRename = razorDocument.Project.Solution.GetDocument(tree).AssumeNotNull();
        var linePosition = node.SyntaxTree.GetText().GetLinePosition(node.SpanStart);
        var workspaceEdit = await Rename.GetRenameEditAsync(documentToRename, linePosition, newName, DisposalToken);
        Assert.NotNull(workspaceEdit);

        var csharpSourceText = await csharpDocument.GetTextAsync(DisposalToken);
        var csharpDocAfterRename = ApplyDocumentEdits(csharpSourceText, csharpDocument.CreateUri(), workspaceEdit);
        AssertEx.EqualOrDiff(expectedCSharpFile, csharpDocAfterRename);

        var razorSourceText = await razorDocument.GetTextAsync(DisposalToken);
        var razorDocAfterRename = ApplyDocumentEdits(razorSourceText, razorDocument.CreateUri(), workspaceEdit);
        AssertEx.EqualOrDiff(expectedRazorFile, razorDocAfterRename);
    }

    private static string ApplyDocumentEdits(SourceText inputText, Uri documentUri, WorkspaceEdit result)
    {
        Assert.True(result.TryGetTextDocumentEdits(out var textDocumentEdits));
        var changes = textDocumentEdits
            .Where(e => e.TextDocument.DocumentUri.GetRequiredParsedUri() == documentUri)
            .SelectMany(e => e.Edits)
            .Select(e => inputText.GetTextChange((TextEdit)e));
        inputText = inputText.WithChanges(changes);

        return inputText.ToString();
    }

    [Export(typeof(ExportableRemoteServiceInvoker))]
    [Export(typeof(IRemoteServiceInvoker))]
    [PartNotDiscoverable]
    private class ExportableRemoteServiceInvoker : IRemoteServiceInvoker
    {
        private IRemoteServiceInvoker? _remoteServiceInvoker;

        internal void SetInvoker(IRemoteServiceInvoker remoteServiceInvoker)
        {
            _remoteServiceInvoker = remoteServiceInvoker;
        }

        public ValueTask<TResult?> TryInvokeAsync<TService, TResult>(Solution solution, Func<TService, RazorPinnedSolutionInfoWrapper, CancellationToken, ValueTask<TResult>> invocation, CancellationToken cancellationToken, [CallerFilePath] string? callerFilePath = null, [CallerMemberName] string? callerMemberName = null) where TService : class
            => _remoteServiceInvoker.AssumeNotNull().TryInvokeAsync(solution, invocation, cancellationToken, callerFilePath, callerMemberName);
    }
}
