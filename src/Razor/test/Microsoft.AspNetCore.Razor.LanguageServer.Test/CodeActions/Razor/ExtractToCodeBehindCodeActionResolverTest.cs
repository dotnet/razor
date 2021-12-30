﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    public class ExtractToCodeBehindCodeActionResolverTest : LanguageServerTestBase
    {
        private readonly DocumentResolver _emptyDocumentResolver;

        private readonly ILogger _logger;

        public ExtractToCodeBehindCodeActionResolverTest()
        {
            _emptyDocumentResolver = new Mock<DocumentResolver>(MockBehavior.Strict).Object;
            Mock.Get(_emptyDocumentResolver).Setup(r => r.TryResolveDocument(It.IsAny<string>(), out It.Ref<DocumentSnapshot?>.IsAny)).Returns(false);

            var logger = new Mock<ILogger>(MockBehavior.Strict).Object;
            Mock.Get(logger).Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>())).Verifiable();
            Mock.Get(logger).Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(false);

            _logger = logger;
        }

        [Fact]
        public async Task Handle_MissingFile()
        {
            // Arrange
            var resolver = new ExtractToCodeBehindCodeActionResolver(LegacyDispatcher, _emptyDocumentResolver, FilePathNormalizer);
            var data = JObject.FromObject(new ExtractToCodeBehindCodeActionParams()
            {
                Uri = new Uri("c:/Test.razor"),
                RemoveStart = 14,
                ExtractStart = 19,
                ExtractEnd = 41,
                RemoveEnd = 41,
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
            var documentPath = "c:\\Test.razor";
            var contents = $"@page \"/test\"{Environment.NewLine}@code {{ private var x = 1; }}";
            var codeDocument = CreateCodeDocument(contents);
            codeDocument.SetUnsupported();

            var resolver = new ExtractToCodeBehindCodeActionResolver(LegacyDispatcher, CreateDocumentResolver(documentPath, codeDocument), FilePathNormalizer);
            var data = JObject.FromObject(new ExtractToCodeBehindCodeActionParams()
            {
                Uri = new Uri("c:/Test.razor"),
                RemoveStart = 14,
                ExtractStart = 20,
                ExtractEnd = 41,
                RemoveEnd = 41,
            });

            // Act
            var workspaceEdit = await resolver.ResolveAsync(data, default);

            // Assert
            Assert.Null(workspaceEdit);
        }

        [Fact]
        public async Task Handle_InvalidFileKind()
        {
            // Arrange
            var documentPath = "c:\\Test.razor";
            var contents = $"@page \"/test\"{Environment.NewLine}@code {{ private var x = 1; }}";
            var codeDocument = CreateCodeDocument(contents);
            codeDocument.SetFileKind(FileKinds.Legacy);

            var resolver = new ExtractToCodeBehindCodeActionResolver(LegacyDispatcher, CreateDocumentResolver(documentPath, codeDocument), FilePathNormalizer);
            var data = JObject.FromObject(new ExtractToCodeBehindCodeActionParams()
            {
                Uri = new Uri("c:/Test.razor"),
                RemoveStart = 14,
                ExtractStart = 20,
                ExtractEnd = 41,
                RemoveEnd = 41,
            });

            // Act
            var workspaceEdit = await resolver.ResolveAsync(data, default);

            // Assert
            Assert.Null(workspaceEdit);
        }

        [Fact]
        public async Task Handle_ExtractCodeBlock()
        {
            // Arrange
            var documentPath = "c:/Test.razor";
            var documentUri = new Uri(documentPath);
            var contents = $"@page \"/test\"{Environment.NewLine}@code {{ private var x = 1; }}";
            var codeDocument = CreateCodeDocument(contents);

            var resolver = new ExtractToCodeBehindCodeActionResolver(LegacyDispatcher, CreateDocumentResolver(documentPath, codeDocument), FilePathNormalizer);
            var actionParams = new ExtractToCodeBehindCodeActionParams
            {
                Uri = documentUri,
                RemoveStart = contents.IndexOf("@code", StringComparison.Ordinal),
                ExtractStart = contents.IndexOf("{", StringComparison.Ordinal),
                ExtractEnd = contents.IndexOf("}", StringComparison.Ordinal),
                RemoveEnd = contents.IndexOf("}", StringComparison.Ordinal),
            };
            var data = JObject.FromObject(actionParams);

            // Act
            var workspaceEdit = await resolver.ResolveAsync(data, default);

            // Assert
            Assert.NotNull(workspaceEdit);
            Assert.NotNull(workspaceEdit.DocumentChanges);
            Assert.Equal(3, workspaceEdit.DocumentChanges!.Count());

            var documentChanges = workspaceEdit.DocumentChanges!.ToArray();
            var createFileChange = documentChanges[0];
            Assert.True(createFileChange.IsCreateFile);

            var editCodeDocumentChange = documentChanges[1];
            Assert.NotNull(editCodeDocumentChange.TextDocumentEdit);
            var editCodeDocumentEdit = editCodeDocumentChange.TextDocumentEdit!.Edits.First();
            Assert.True(editCodeDocumentEdit.Range.Start.TryGetAbsoluteIndex(codeDocument.GetSourceText(), _logger, out var removeStart));
            Assert.Equal(actionParams.RemoveStart, removeStart);
            Assert.True(editCodeDocumentEdit.Range.End.TryGetAbsoluteIndex(codeDocument.GetSourceText(), _logger, out var removeEnd));
            Assert.Equal(actionParams.RemoveEnd, removeEnd);

            var editCodeBehindChange = documentChanges[2];
            Assert.NotNull(editCodeBehindChange.TextDocumentEdit);
            var editCodeBehindEdit = editCodeBehindChange.TextDocumentEdit!.Edits.First();
            Assert.Contains("public partial class Test", editCodeBehindEdit.NewText, StringComparison.Ordinal);
            Assert.Contains("private var x = 1", editCodeBehindEdit.NewText, StringComparison.Ordinal);
            Assert.Contains("namespace test.Pages", editCodeBehindEdit.NewText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Handle_ExtractFunctionsBlock()
        {
            // Arrange
            var documentPath = "c:/Test.razor";
            var documentUri = new Uri(documentPath);
            var contents = $"@page \"/test\"{Environment.NewLine}@functions {{ private var x = 1; }}";
            var codeDocument = CreateCodeDocument(contents);

            var resolver = new ExtractToCodeBehindCodeActionResolver(LegacyDispatcher, CreateDocumentResolver(documentPath, codeDocument), FilePathNormalizer);
            var actionParams = new ExtractToCodeBehindCodeActionParams
            {
                Uri = documentUri,
                RemoveStart = contents.IndexOf("@functions", StringComparison.Ordinal),
                ExtractStart = contents.IndexOf("{", StringComparison.Ordinal),
                ExtractEnd = contents.IndexOf("}", StringComparison.Ordinal),
                RemoveEnd = contents.IndexOf("}", StringComparison.Ordinal),
            };
            var data = JObject.FromObject(actionParams);

            // Act
            var workspaceEdit = await resolver.ResolveAsync(data, default);

            // Assert
            Assert.NotNull(workspaceEdit);
            Assert.NotNull(workspaceEdit.DocumentChanges);
            Assert.Equal(3, workspaceEdit.DocumentChanges!.Count());

            var documentChanges = workspaceEdit.DocumentChanges!.ToArray();
            var createFileChange = documentChanges[0];
            Assert.True(createFileChange.IsCreateFile);

            var editCodeDocumentChange = documentChanges[1];
            Assert.NotNull(editCodeDocumentChange.TextDocumentEdit);
            var editCodeDocumentEdit = editCodeDocumentChange.TextDocumentEdit!.Edits.First();
            Assert.True(editCodeDocumentEdit.Range.Start.TryGetAbsoluteIndex(codeDocument.GetSourceText(), _logger, out var removeStart));
            Assert.Equal(actionParams.RemoveStart, removeStart);
            Assert.True(editCodeDocumentEdit.Range.End.TryGetAbsoluteIndex(codeDocument.GetSourceText(), _logger, out var removeEnd));
            Assert.Equal(actionParams.RemoveEnd, removeEnd);

            var editCodeBehindChange = documentChanges[2];
            Assert.NotNull(editCodeBehindChange.TextDocumentEdit);
            var editCodeBehindEdit = editCodeBehindChange.TextDocumentEdit!.Edits.First();
            Assert.Contains("public partial class Test", editCodeBehindEdit.NewText, StringComparison.Ordinal);
            Assert.Contains("private var x = 1", editCodeBehindEdit.NewText, StringComparison.Ordinal);
            Assert.Contains("namespace test.Pages", editCodeBehindEdit.NewText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Handle_ExtractCodeBlockWithUsing()
        {
            // Arrange
            var documentPath = "c:/Test.razor";
            var documentUri = new Uri(documentPath);
            var contents = $"@page \"/test\"\n@using System.Diagnostics{Environment.NewLine}@code {{ private var x = 1; }}";
            var codeDocument = CreateCodeDocument(contents);

            var resolver = new ExtractToCodeBehindCodeActionResolver(LegacyDispatcher, CreateDocumentResolver(documentPath, codeDocument), FilePathNormalizer);
            var actionParams = new ExtractToCodeBehindCodeActionParams
            {
                Uri = documentUri,
                RemoveStart = contents.IndexOf("@code", StringComparison.Ordinal),
                ExtractStart = contents.IndexOf("{", StringComparison.Ordinal),
                ExtractEnd = contents.IndexOf("}", StringComparison.Ordinal),
                RemoveEnd = contents.IndexOf("}", StringComparison.Ordinal),
            };
            var data = JObject.FromObject(actionParams);

            // Act
            var workspaceEdit = await resolver.ResolveAsync(data, default);

            // Assert
            Assert.NotNull(workspaceEdit);
            Assert.NotNull(workspaceEdit.DocumentChanges);
            Assert.Equal(3, workspaceEdit.DocumentChanges!.Count());

            var documentChanges = workspaceEdit.DocumentChanges!.ToArray();
            var createFileChange = documentChanges[0];
            Assert.True(createFileChange.IsCreateFile);

            var editCodeDocumentChange = documentChanges[1];
            Assert.NotNull(editCodeDocumentChange.TextDocumentEdit);
            var editCodeDocumentEdit = editCodeDocumentChange.TextDocumentEdit!.Edits.First();
            Assert.True(editCodeDocumentEdit.Range.Start.TryGetAbsoluteIndex(codeDocument.GetSourceText(), _logger, out var removeStart));
            Assert.Equal(actionParams.RemoveStart, removeStart);
            Assert.True(editCodeDocumentEdit.Range.End.TryGetAbsoluteIndex(codeDocument.GetSourceText(), _logger, out var removeEnd));
            Assert.Equal(actionParams.RemoveEnd, removeEnd);

            var editCodeBehindChange = documentChanges[2];
            Assert.NotNull(editCodeBehindChange.TextDocumentEdit);
            var editCodeBehindEdit = editCodeBehindChange.TextDocumentEdit!.Edits.First();
            Assert.Contains("using System.Diagnostics", editCodeBehindEdit.NewText, StringComparison.Ordinal);
            Assert.Contains("public partial class Test", editCodeBehindEdit.NewText, StringComparison.Ordinal);
            Assert.Contains("private var x = 1", editCodeBehindEdit.NewText, StringComparison.Ordinal);
            Assert.Contains("namespace test.Pages", editCodeBehindEdit.NewText, StringComparison.Ordinal);
        }

        private static DocumentResolver CreateDocumentResolver(string documentPath, RazorCodeDocument codeDocument)
        {
            var sourceTextChars = new char[codeDocument.Source.Length];
            codeDocument.Source.CopyTo(0, sourceTextChars, 0, codeDocument.Source.Length);
            var sourceText = SourceText.From(new string(sourceTextChars));
            var documentSnapshot = Mock.Of<DocumentSnapshot>(document =>
                document.GetGeneratedOutputAsync() == Task.FromResult(codeDocument) &&
                document.GetTextAsync() == Task.FromResult(sourceText), MockBehavior.Strict);
            var documentResolver = new Mock<DocumentResolver>(MockBehavior.Strict);
            documentResolver
                .Setup(resolver => resolver.TryResolveDocument(documentPath, out documentSnapshot))
                .Returns(true);
            documentResolver
                .Setup(resolver => resolver.TryResolveDocument(It.IsNotIn(documentPath), out documentSnapshot))
                .Returns(false);
            return documentResolver.Object;
        }

        private static RazorCodeDocument CreateCodeDocument(string text)
        {
            var projectItem = new TestRazorProjectItem("c:/Test.razor", "c:/Test.razor", "Test.razor") { Content = text };
            var projectEngine = RazorProjectEngine.Create(RazorConfiguration.Default, TestRazorProjectFileSystem.Empty, (builder) => builder.SetRootNamespace("test.Pages"));

            var codeDocument = projectEngine.Process(projectItem);
            codeDocument.SetFileKind(FileKinds.Component);

            return codeDocument;
        }
    }
}
