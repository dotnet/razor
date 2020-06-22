using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Refactoring;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Moq;
using Xunit;
using System;
using Newtonsoft.Json.Linq;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Refactoring
{
    public class ExtractToCodeBehindCodeActionResolverTest : LanguageServerTestBase
    {
        private readonly DocumentResolver EmptyDocumentResolver = Mock.Of<DocumentResolver>();

        [Fact]
        public async Task Handle_MissingFile()
        {
            // Arrange
            var resolver = new ExtractToCodeBehindCodeActionResolver(new DefaultForegroundDispatcher(), EmptyDocumentResolver);
            var data = JObject.FromObject(new ExtractToCodeBehindParams()
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
            var contents = "@page \"/test\"\n@code { private var x = 1; }";
            var codeDocument = CreateCodeDocument(contents);
            codeDocument.SetUnsupported();

            var resolver = new ExtractToCodeBehindCodeActionResolver(new DefaultForegroundDispatcher(), CreateDocumentResolver(documentPath, codeDocument));
            var data = JObject.FromObject(new ExtractToCodeBehindParams()
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
            var contents = "@page \"/test\"\n@code { private var x = 1; }";
            var codeDocument = CreateCodeDocument(contents);
            codeDocument.SetFileKind(FileKinds.Legacy);

            var resolver = new ExtractToCodeBehindCodeActionResolver(new DefaultForegroundDispatcher(), CreateDocumentResolver(documentPath, codeDocument));
            var data = JObject.FromObject(new ExtractToCodeBehindParams()
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
            var documentPath = "c:\\Test.razor";
            var documentUri = new Uri(documentPath);
            var contents = "@page \"/test\"\n@code { private var x = 1; }";
            var codeDocument = CreateCodeDocument(contents);

            var resolver = new ExtractToCodeBehindCodeActionResolver(new DefaultForegroundDispatcher(), CreateDocumentResolver(documentPath, codeDocument));
            var data = JObject.FromObject(new ExtractToCodeBehindParams()
            {
                Uri = documentUri,
                RemoveStart = 14,
                ExtractStart = 20,
                ExtractEnd = 41,
                RemoveEnd = 41,
            });

            // Act
            var workspaceEdit = await resolver.ResolveAsync(data, default);

            // Assert
            Assert.NotNull(workspaceEdit);
            var documentChanges = workspaceEdit.Changes[documentUri];
            workspaceEdit.Changes.Remove(documentUri);
            Assert.Single(documentChanges);
            Assert.Equal(14, documentChanges.First().Range.Start.GetAbsoluteIndex(codeDocument.GetSourceText()));
            Assert.Equal(41, documentChanges.First().Range.End.GetAbsoluteIndex(codeDocument.GetSourceText()));
            var codeBehindChanges = workspaceEdit.Changes.Values.First();
            Assert.Single(codeBehindChanges);
            Assert.Contains("public partial class Test", codeBehindChanges.First().NewText);
            Assert.Contains("private var x = 1", codeBehindChanges.First().NewText);
            Assert.Contains("namespace test.Pages", codeBehindChanges.First().NewText);
        }

        [Fact]
        public async Task Handle_ExtractFunctionsBlock()
        {
            // Arrange
            var documentPath = "c:\\Test.razor";
            var documentUri = new Uri(documentPath);
            var contents = "@page \"/test\"\n@functions { private var x = 1; }";
            var codeDocument = CreateCodeDocument(contents);

            var resolver = new ExtractToCodeBehindCodeActionResolver(new DefaultForegroundDispatcher(), CreateDocumentResolver(documentPath, codeDocument));
            var data = JObject.FromObject(new ExtractToCodeBehindParams()
            {
                Uri = documentUri,
                RemoveStart = 14,
                ExtractStart = 24,
                ExtractEnd = 46,
                RemoveEnd = 46,
            });

            // Act
            var workspaceEdit = await resolver.ResolveAsync(data, default);

            // Assert
            Assert.NotNull(workspaceEdit);
            var documentChanges = workspaceEdit.Changes[documentUri];
            workspaceEdit.Changes.Remove(documentUri);
            Assert.Single(documentChanges);
            Assert.Equal(14, documentChanges.First().Range.Start.GetAbsoluteIndex(codeDocument.GetSourceText()));
            Assert.Equal(46, documentChanges.First().Range.End.GetAbsoluteIndex(codeDocument.GetSourceText()));
            var codeBehindChanges = workspaceEdit.Changes.Values.First();
            Assert.Single(codeBehindChanges);
            Assert.Contains("public partial class Test", codeBehindChanges.First().NewText);
            Assert.Contains("private var x = 1", codeBehindChanges.First().NewText);
            Assert.Contains("namespace test.Pages", codeBehindChanges.First().NewText);
        }

        private static DocumentResolver CreateDocumentResolver(string documentPath, RazorCodeDocument codeDocument)
        {
            var sourceTextChars = new char[codeDocument.Source.Length];
            codeDocument.Source.CopyTo(0, sourceTextChars, 0, codeDocument.Source.Length);
            var sourceText = SourceText.From(new string(sourceTextChars));
            var documentSnapshot = Mock.Of<DocumentSnapshot>(document =>
                document.GetGeneratedOutputAsync() == Task.FromResult(codeDocument) &&
                document.GetTextAsync() == Task.FromResult(sourceText));
            var documentResolver = new Mock<DocumentResolver>();
            documentResolver
                .Setup(resolver => resolver.TryResolveDocument(documentPath, out documentSnapshot))
                .Returns(true);
            return documentResolver.Object;
        }

        private static RazorCodeDocument CreateCodeDocument(string text)
        {
            var projectItem = new TestRazorProjectItem("c:\\Test.razor", "c:\\Test.razor", "Test.razor") { Content = text };
            var projectEngine = RazorProjectEngine.Create(RazorConfiguration.Default, TestRazorProjectFileSystem.Empty, (builder) =>
            {
                builder.SetRootNamespace("test.Pages");
            });

            var codeDocument = projectEngine.Process(projectItem);
            codeDocument.SetFileKind(FileKinds.Component);

            return codeDocument;
        }
    }
}
