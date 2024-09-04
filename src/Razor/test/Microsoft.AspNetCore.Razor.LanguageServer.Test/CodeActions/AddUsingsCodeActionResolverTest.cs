﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;

public class AddUsingsCodeActionResolverTest(ITestOutputHelper testOutput) : LanguageServerTestBase(testOutput)
{
    private readonly IDocumentContextFactory _emptyDocumentContextFactory = new TestDocumentContextFactory();

    [Fact]
    public void GetNamespaceFromFQN_Invalid_ReturnsEmpty()
    {
        // Arrange
        var fqn = "Abc";

        // Act
        var namespaceName = AddUsingsCodeActionResolver.GetNamespaceFromFQN(fqn);

        // Assert
        Assert.Empty(namespaceName);
    }

    [Fact]
    public void GetNamespaceFromFQN_Valid_ReturnsNamespace()
    {
        // Arrange
        var fqn = "Abc.Xyz";

        // Act
        var namespaceName = AddUsingsCodeActionResolver.GetNamespaceFromFQN(fqn);

        // Assert
        Assert.Equal("Abc", namespaceName);
    }

    [Fact]
    public void TryCreateAddUsingResolutionParams_CreatesResolutionParams()
    {
        // Arrange
        var fqn = "Abc.Xyz";
        var docUri = new Uri("c:/path");

        // Act
        var result = AddUsingsCodeActionResolver.TryCreateAddUsingResolutionParams(fqn, docUri, additionalEdit: null, out var @namespace, out var resolutionParams);

        // Assert
        Assert.True(result);
        Assert.Equal("Abc", @namespace);
        Assert.NotNull(resolutionParams);
    }

    [Fact]
    public async Task Handle_MissingFile()
    {
        // Arrange
        var resolver = new AddUsingsCodeActionResolver(_emptyDocumentContextFactory);
        var data = JsonSerializer.SerializeToElement(new AddUsingsCodeActionParams()
        {
            Uri = new Uri("c:/Test.razor"),
            Namespace = "System",
        });

        // Act
        var workspaceEdit = await resolver.ResolveAsync(data, default);

        // Assert
        Assert.Null(workspaceEdit);
    }

    [Fact]
    public async Task Handle_Unsupported()
    {
        // Arrange
        var documentPath = new Uri("c:/Test.razor");
        var contents = "@page \"/test\"";
        var codeDocument = CreateCodeDocument(contents);
        codeDocument.SetUnsupported();

        var resolver = new AddUsingsCodeActionResolver(CreateDocumentContextFactory(documentPath, codeDocument));
        var data = JsonSerializer.SerializeToElement(new AddUsingsCodeActionParams()
        {
            Uri = documentPath,
            Namespace = "System",
        });

        // Act
        var workspaceEdit = await resolver.ResolveAsync(data, default);

        // Assert
        Assert.Null(workspaceEdit);
    }

    [Fact]
    public async Task Handle_AddOneUsingToEmpty()
    {
        // Arrange
        var documentPath = new Uri("c:/Test.razor");
        var contents = string.Empty;
        var codeDocument = CreateCodeDocument(contents);

        var resolver = new AddUsingsCodeActionResolver(CreateDocumentContextFactory(documentPath, codeDocument));
        var actionParams = new AddUsingsCodeActionParams
        {
            Uri = documentPath,
            Namespace = "System",
        };
        var data = JsonSerializer.SerializeToElement(actionParams);

        // Act
        var workspaceEdit = await resolver.ResolveAsync(data, default);

        // Assert
        Assert.NotNull(workspaceEdit);
        Assert.NotNull(workspaceEdit.DocumentChanges);
        Assert.Equal(1, workspaceEdit.DocumentChanges.Value.Count());

        var addUsingsChange = workspaceEdit.DocumentChanges.Value.First();
        Assert.True(addUsingsChange.TryGetFirst(out var textDocumentEdit));
        Assert.Single(textDocumentEdit.Edits);
        var firstEdit = textDocumentEdit.Edits.First();
        Assert.Equal(0, firstEdit.Range.Start.Line);
        Assert.Equal($"""
            @using System

            """, firstEdit.NewText);
    }

    [Fact]
    public async Task Handle_AddOneUsingToComponentPageDirective()
    {
        // Arrange
        var documentPath = new Uri("c:/Test.razor");
        var contents = $"""
            @page "/"

            """;
        var codeDocument = CreateCodeDocument(contents);

        var resolver = new AddUsingsCodeActionResolver(CreateDocumentContextFactory(documentPath, codeDocument));
        var actionParams = new AddUsingsCodeActionParams
        {
            Uri = documentPath,
            Namespace = "System",
        };
        var data = JsonSerializer.SerializeToElement(actionParams);

        // Act
        var workspaceEdit = await resolver.ResolveAsync(data, default);

        // Assert
        Assert.NotNull(workspaceEdit);
        Assert.NotNull(workspaceEdit.DocumentChanges);
        Assert.Equal(1, workspaceEdit.DocumentChanges.Value.Count());

        var addUsingsChange = workspaceEdit.DocumentChanges.Value.First();
        Assert.True(addUsingsChange.TryGetFirst(out var textDocumentEdit));
        var firstEdit = Assert.Single(textDocumentEdit.Edits);
        Assert.Equal(1, firstEdit.Range.Start.Line);
        Assert.Equal($"""
            @using System

            """, firstEdit.NewText);
    }

    [Fact]
    public async Task Handle_AddOneUsingToPageDirective()
    {
        // Arrange
        var documentPath = new Uri("c:/Test.cshtml");
        var contents = $"""
            @page
            @model IndexModel
            """;

        var projectItem = new TestRazorProjectItem("c:/Test.cshtml", "c:/Test.cshtml", "Test.cshtml") { Content = contents };
        var projectEngine = RazorProjectEngine.Create(RazorConfiguration.Default, TestRazorProjectFileSystem.Empty, (builder) =>
        {
            PageDirective.Register(builder);
            ModelDirective.Register(builder);
        });
        var codeDocument = projectEngine.Process(projectItem);
        codeDocument.SetFileKind(FileKinds.Legacy);

        var resolver = new AddUsingsCodeActionResolver(CreateDocumentContextFactory(documentPath, codeDocument));
        var actionParams = new AddUsingsCodeActionParams
        {
            Uri = documentPath,
            Namespace = "System",
        };
        var data = JsonSerializer.SerializeToElement(actionParams);

        // Act
        var workspaceEdit = await resolver.ResolveAsync(data, default);

        // Assert
        Assert.NotNull(workspaceEdit);
        Assert.NotNull(workspaceEdit.DocumentChanges);
        Assert.Equal(1, workspaceEdit.DocumentChanges.Value.Count());

        var addUsingsChange = workspaceEdit.DocumentChanges.Value.First();
        Assert.True(addUsingsChange.TryGetFirst(out var textDocumentEdit));
        var firstEdit = Assert.Single(textDocumentEdit.Edits);
        Assert.Equal(1, firstEdit.Range.Start.Line);
        Assert.Equal($"""
            @using System

            """, firstEdit.NewText);
    }

    [Fact]
    public async Task Handle_AddOneUsingToHTML()
    {
        // Arrange
        var documentPath = new Uri("c:/Test.razor");
        var contents = $"""
            <table>
            <tr>
            </tr>
            </table>
            """;
        var codeDocument = CreateCodeDocument(contents);

        var resolver = new AddUsingsCodeActionResolver(CreateDocumentContextFactory(documentPath, codeDocument));
        var actionParams = new AddUsingsCodeActionParams
        {
            Uri = documentPath,
            Namespace = "System",
        };
        var data = JsonSerializer.SerializeToElement(actionParams);

        // Act
        var workspaceEdit = await resolver.ResolveAsync(data, default);

        // Assert
        Assert.NotNull(workspaceEdit);
        Assert.NotNull(workspaceEdit.DocumentChanges);
        Assert.Equal(1, workspaceEdit.DocumentChanges.Value.Count());

        var addUsingsChange = workspaceEdit.DocumentChanges.Value.First();
        Assert.True(addUsingsChange.TryGetFirst(out var textDocumentEdit));
        var firstEdit = Assert.Single(textDocumentEdit.Edits);
        Assert.Equal(0, firstEdit.Range.Start.Line);
        Assert.Equal($"""
            @using System

            """, firstEdit.NewText);
    }

    [Fact]
    public async Task Handle_AddOneUsingToNamespace()
    {
        // Arrange
        var documentPath = new Uri("c:/Test.razor");
        var contents = $"""
            @namespace Testing

            """;
        var codeDocument = CreateCodeDocument(contents);

        var resolver = new AddUsingsCodeActionResolver(CreateDocumentContextFactory(documentPath, codeDocument));
        var actionParams = new AddUsingsCodeActionParams
        {
            Uri = documentPath,
            Namespace = "System",
        };
        var data = JsonSerializer.SerializeToElement(actionParams);

        // Act
        var workspaceEdit = await resolver.ResolveAsync(data, default);

        // Assert
        Assert.NotNull(workspaceEdit);
        Assert.NotNull(workspaceEdit.DocumentChanges);
        Assert.Equal(1, workspaceEdit.DocumentChanges.Value.Count());

        var addUsingsChange = workspaceEdit.DocumentChanges.Value.First();
        Assert.True(addUsingsChange.TryGetFirst(out var textDocumentEdit));
        var firstEdit = Assert.Single(textDocumentEdit.Edits);
        Assert.Equal(1, firstEdit.Range.Start.Line);
        Assert.Equal($"""
            @using System

            """, firstEdit.NewText);
    }

    [Fact]
    public async Task Handle_AddOneUsingToPageAndNamespace()
    {
        // Arrange
        var documentPath = new Uri("c:/Test.razor");
        var contents = $"""
            @page "/"
            @namespace Testing

            """;
        var codeDocument = CreateCodeDocument(contents);

        var resolver = new AddUsingsCodeActionResolver(CreateDocumentContextFactory(documentPath, codeDocument));
        var actionParams = new AddUsingsCodeActionParams
        {
            Uri = documentPath,
            Namespace = "System",
        };
        var data = JsonSerializer.SerializeToElement(actionParams);

        // Act
        var workspaceEdit = await resolver.ResolveAsync(data, default);

        // Assert
        Assert.NotNull(workspaceEdit);
        Assert.NotNull(workspaceEdit.DocumentChanges);
        Assert.Equal(1, workspaceEdit.DocumentChanges.Value.Count());

        var addUsingsChange = workspaceEdit.DocumentChanges.Value.First();
        Assert.True(addUsingsChange.TryGetFirst(out var textDocumentEdit));
        var firstEdit = Assert.Single(textDocumentEdit.Edits);
        Assert.Equal(2, firstEdit.Range.Start.Line);
        Assert.Equal($"""
            @using System

            """, firstEdit.NewText);
    }

    [Fact]
    public async Task Handle_AddOneUsingToUsings()
    {
        // Arrange
        var documentPath = new Uri("c:/Test.razor");
        var contents = $"@using System";
        var codeDocument = CreateCodeDocument(contents);

        var resolver = new AddUsingsCodeActionResolver(CreateDocumentContextFactory(documentPath, codeDocument));
        var actionParams = new AddUsingsCodeActionParams
        {
            Uri = documentPath,
            Namespace = "System.Linq",
        };
        var data = JsonSerializer.SerializeToElement(actionParams);

        // Act
        var workspaceEdit = await resolver.ResolveAsync(data, default);

        // Assert
        Assert.NotNull(workspaceEdit);
        Assert.NotNull(workspaceEdit.DocumentChanges);
        Assert.Equal(1, workspaceEdit.DocumentChanges.Value.Count());

        var addUsingsChange = workspaceEdit.DocumentChanges.Value.First();
        Assert.True(addUsingsChange.TryGetFirst(out var textDocumentEdit));
        var firstEdit = Assert.Single(textDocumentEdit.Edits);
        Assert.Equal(1, firstEdit.Range.Start.Line);
        Assert.Equal($"""
            @using System.Linq

            """, firstEdit.NewText);
    }

    [Fact]
    public async Task Handle_AddOneNonSystemUsingToSystemUsings()
    {
        // Arrange
        var documentPath = new Uri("c:/Test.razor");
        var contents = $"""
            @using System
            @using System.Linq

            """;
        var codeDocument = CreateCodeDocument(contents);

        var resolver = new AddUsingsCodeActionResolver(CreateDocumentContextFactory(documentPath, codeDocument));
        var actionParams = new AddUsingsCodeActionParams
        {
            Uri = documentPath,
            Namespace = "Microsoft.AspNetCore.Razor.Language",
        };
        var data = JsonSerializer.SerializeToElement(actionParams);

        // Act
        var workspaceEdit = await resolver.ResolveAsync(data, default);

        // Assert
        Assert.NotNull(workspaceEdit);
        Assert.NotNull(workspaceEdit.DocumentChanges);
        Assert.Equal(1, workspaceEdit.DocumentChanges.Value.Count());

        var addUsingsChange = workspaceEdit.DocumentChanges.Value.First();
        Assert.True(addUsingsChange.TryGetFirst(out var textDocumentEdit));
        var firstEdit = Assert.Single(textDocumentEdit.Edits);
        Assert.Equal(2, firstEdit.Range.Start.Line);
        Assert.Equal($"""
            @using Microsoft.AspNetCore.Razor.Language

            """, firstEdit.NewText);
    }

    private static RazorCodeDocument CreateCodeDocument(string text)
    {
        var fileName = "Test.razor";
        var filePath = $"c:/{fileName}";
        var projectItem = new TestRazorProjectItem(filePath, filePath, fileName) { Content = text };
        var projectEngine = RazorProjectEngine.Create(RazorConfiguration.Default, TestRazorProjectFileSystem.Empty, (builder) => PageDirective.Register(builder));
        var codeDocument = projectEngine.Process(projectItem);
        codeDocument.SetFileKind(FileKinds.Component);
        return codeDocument;
    }
}
