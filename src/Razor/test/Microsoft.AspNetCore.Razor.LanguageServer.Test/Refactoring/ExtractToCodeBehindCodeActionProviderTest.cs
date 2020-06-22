using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Refactoring;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Moq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using System;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Refactoring
{
    public class ExtractToCodeBehindCodeActionProviderTest : LanguageServerTestBase
    {
        [Fact]
        public async Task Handle_InvalidFileKind()
        {
            // Arrange
            var documentPath = "c:/Test.razor";
            var contents = "@page \"/test\"\n@code {}";
            var codeDocument = CreateCodeDocument(contents);
            codeDocument.SetFileKind(FileKinds.Legacy);

            var request = new CodeActionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Range = new Range(),
            };

            var location = new SourceLocation(contents.IndexOf("code"), -1, -1);
            var provider = new ExtractToCodeBehindCodeActionProvider();
            var context = new RazorCodeActionContext(request, codeDocument, location);

            // Act
            var commandOrCodeActionContainer = await provider.ProvideAsync(context, default);

            // Assert
            Assert.Null(commandOrCodeActionContainer);
        }

        [Fact]
        public async Task Handle_Unsupported()
        {
            // Arrange
            var documentPath = "c:/Test.razor";
            var contents = "@page \"/test\"\n@code {}";
            var codeDocument = CreateCodeDocument(contents);
            codeDocument.SetUnsupported();

            var request = new CodeActionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Range = new Range(),
            };

            var location = new SourceLocation(contents.IndexOf("code"), -1, -1);
            var provider = new ExtractToCodeBehindCodeActionProvider();
            var context = new RazorCodeActionContext(request, codeDocument, location);

            // Act
            var commandOrCodeActionContainer = await provider.ProvideAsync(context, default);

            // Assert
            Assert.Null(commandOrCodeActionContainer);
        }

        [Fact]
        public async Task Handle_OutsideCodeDirective()
        {
            // Arrange
            var documentPath = "c:/Test.razor";
            var contents = "@page \"/test\"\n@code {}";
            var codeDocument = CreateCodeDocument(contents);

            var request = new CodeActionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Range = new Range(),
            };

            var location = new SourceLocation(contents.IndexOf("test"), -1, -1);
            var provider = new ExtractToCodeBehindCodeActionProvider();
            var context = new RazorCodeActionContext(request, codeDocument, location);

            // Act
            var commandOrCodeActionContainer = await provider.ProvideAsync(context, default);

            // Assert
            Assert.Null(commandOrCodeActionContainer);
        }

        [Fact]
        public async Task Handle_InCodeDirectiveBlock()
        {
            // Arrange
            var documentPath = "c:/Test.razor";
            var contents = "@page \"/test\"\n@code {}";
            var codeDocument = CreateCodeDocument(contents);

            var request = new CodeActionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Range = new Range(),
            };

            var location = new SourceLocation(contents.IndexOf("code") + 6, -1, -1);
            var provider = new ExtractToCodeBehindCodeActionProvider();
            var context = new RazorCodeActionContext(request, codeDocument, location);

            // Act
            var commandOrCodeActionContainer = await provider.ProvideAsync(context, default);

            // Assert
            Assert.Null(commandOrCodeActionContainer);
        }

        [Fact]
        public async Task Handle_InCodeDirectiveMalformed()
        {
            // Arrange
            var documentPath = "c:/Test.razor";
            var contents = "@page \"/test\"\n@code";
            var codeDocument = CreateCodeDocument(contents);

            var request = new CodeActionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Range = new Range(),
            };

            var location = new SourceLocation(contents.IndexOf("code"), -1, -1);
            var provider = new ExtractToCodeBehindCodeActionProvider();
            var context = new RazorCodeActionContext(request, codeDocument, location);

            // Act
            var commandOrCodeActionContainer = await provider.ProvideAsync(context, default);

            // Assert
            Assert.Null(commandOrCodeActionContainer);
        }

        [Fact]
        public async Task Handle_InCodeDirectiveWithMarkup()
        {
            // Arrange
            var documentPath = "c:/Test.razor";
            var contents = "@page \"/test\"\n@code { void Test() { <h1>Hello, world!</h1> } }";
            var codeDocument = CreateCodeDocument(contents);

            var request = new CodeActionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Range = new Range(),
            };

            var location = new SourceLocation(contents.IndexOf("code"), -1, -1);
            var provider = new ExtractToCodeBehindCodeActionProvider();
            var context = new RazorCodeActionContext(request, codeDocument, location);

            // Act
            var commandOrCodeActionContainer = await provider.ProvideAsync(context, default);

            // Assert
            Assert.Null(commandOrCodeActionContainer);
        }

        [Fact]
        public async Task Handle_InCodeDirective()
        {
            // Arrange
            var documentPath = "c:/Test.razor";
            var contents = "@page \"/test\"\n@code { private var x = 1; }";
            var codeDocument = CreateCodeDocument(contents);

            var request = new CodeActionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Range = new Range(),
            };

            var location = new SourceLocation(contents.IndexOf("code"), -1, -1);
            var provider = new ExtractToCodeBehindCodeActionProvider();
            var context = new RazorCodeActionContext(request, codeDocument, location);

            // Act
            var commandOrCodeActionContainer = await provider.ProvideAsync(context, default);

            // Assert
            Assert.Single(commandOrCodeActionContainer);
            var actionParams = commandOrCodeActionContainer
                .First().Command.Arguments[0]
                .ToObject<RazorCodeActionResolutionParams>().Data
                .ToObject<ExtractToCodeBehindParams>();
            Assert.Equal(14, actionParams.RemoveStart);
            Assert.Equal(19, actionParams.ExtractStart);
            Assert.Equal(42, actionParams.ExtractEnd);
            Assert.Equal(42, actionParams.RemoveEnd);
        }

        [Fact]
        public async Task Handle_InFunctionsDirective()
        {
            // Arrange
            var documentPath = "c:/Test.razor";
            var contents = "@page \"/test\"\n@functions { private var x = 1; }";
            var codeDocument = CreateCodeDocument(contents);

            var request = new CodeActionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Range = new Range(),
            };

            var location = new SourceLocation(contents.IndexOf("functions"), -1, -1);
            var provider = new ExtractToCodeBehindCodeActionProvider();
            var context = new RazorCodeActionContext(request, codeDocument, location);

            // Act
            var commandOrCodeActionContainer = await provider.ProvideAsync(context, default);

            // Assert
            Assert.Single(commandOrCodeActionContainer);
            var actionParams = commandOrCodeActionContainer
                .First().Command.Arguments[0]
                .ToObject<RazorCodeActionResolutionParams>().Data
                .ToObject<ExtractToCodeBehindParams>();
            Assert.Equal(14, actionParams.RemoveStart);
            Assert.Equal(24, actionParams.ExtractStart);
            Assert.Equal(47, actionParams.ExtractEnd);
            Assert.Equal(47, actionParams.RemoveEnd);
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
            var codeDocument = TestRazorCodeDocument.CreateEmpty();
            codeDocument.SetFileKind(FileKinds.Component);

            var sourceDocument = TestRazorSourceDocument.Create(text, filePath: "c:/Test.razor", relativePath: "c:/Test.razor");
            var options = RazorParserOptions.Create(o =>
            {
                o.Directives.Add(ComponentCodeDirective.Directive);
                o.Directives.Add(FunctionsDirective.Directive);
            });
            var syntaxTree = RazorSyntaxTree.Parse(sourceDocument, options);
            codeDocument.SetSyntaxTree(syntaxTree);

            return codeDocument;
        }
    }
}
