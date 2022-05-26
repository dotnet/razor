// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    public abstract class FormattingLanguageServerTestBase : LanguageServerTestBase
    {
        public FormattingLanguageServerTestBase()
        {
            EmptyDocumentResolver = Mock.Of<DocumentResolver>(r => r.TryResolveDocument(It.IsAny<string>(), out It.Ref<DocumentSnapshot?>.IsAny) == false, MockBehavior.Strict);
        }

        internal DocumentResolver EmptyDocumentResolver { get; }

        internal static RazorCodeDocument CreateCodeDocument(string content, IReadOnlyList<SourceMapping> sourceMappings)
        {
            var sourceDocument = TestRazorSourceDocument.Create(content);
            var codeDocument = RazorCodeDocument.Create(sourceDocument);
            var syntaxTree = RazorSyntaxTree.Parse(sourceDocument, RazorParserOptions.CreateDefault());
            var razorCSharpDocument = RazorCSharpDocument.Create(
                content, RazorCodeGenerationOptions.CreateDefault(), Array.Empty<RazorDiagnostic>(), sourceMappings, Array.Empty<LinePragma>());
            codeDocument.SetSyntaxTree(syntaxTree);
            codeDocument.SetCSharpDocument(razorCSharpDocument);

            return codeDocument;
        }

        internal static IOptionsMonitor<RazorLSPOptions> GetOptionsMonitor(bool enableFormatting)
        {
            var monitor = new Mock<IOptionsMonitor<RazorLSPOptions>>(MockBehavior.Strict);
            monitor.SetupGet(m => m.CurrentValue).Returns(new RazorLSPOptions(default, enableFormatting, true, insertSpaces: true, tabSize: 4));
            return monitor.Object;
        }

        internal static DocumentResolver CreateDocumentResolver(string documentPath, RazorCodeDocument codeDocument)
        {
            var sourceTextChars = new char[codeDocument.Source.Length];
            codeDocument.Source.CopyTo(0, sourceTextChars, 0, codeDocument.Source.Length);
            var sourceText = SourceText.From(new string(sourceTextChars));
            var documentSnapshot = Mock.Of<DocumentSnapshot>(document =>
                document.GetGeneratedOutputAsync() == Task.FromResult(codeDocument) &&
                document.GetTextAsync() == Task.FromResult(sourceText), MockBehavior.Strict);
            var documentResolver = new Mock<DocumentResolver>(MockBehavior.Strict);
            documentResolver.Setup(resolver => resolver.TryResolveDocument(documentPath, out documentSnapshot))
                .Returns(true);
            documentResolver.Setup(resolver => resolver.TryResolveDocument(It.IsNotIn(documentPath), out documentSnapshot))
                .Returns(false);
            return documentResolver.Object;
        }

        internal class TestRazorFormattingService : RazorFormattingService
        {
            public bool Called { get; private set; }

            public override Task<TextEdit[]> FormatAsync(Uri uri, DocumentSnapshot documentSnapshot, VisualStudio.LanguageServer.Protocol.Range range, FormattingOptions options, CancellationToken cancellationToken)
            {
                Called = true;
                return Task.FromResult(Array.Empty<TextEdit>());
            }

            public override Task<TextEdit[]> FormatCodeActionAsync(Uri uri, DocumentSnapshot documentSnapshot, RazorLanguageKind kind, TextEdit[] formattedEdits, FormattingOptions options, CancellationToken cancellationToken)
            {
                return Task.FromResult(formattedEdits);
            }

            public override Task<TextEdit[]> FormatOnTypeAsync(Uri uri, DocumentSnapshot documentSnapshot, RazorLanguageKind kind, TextEdit[] formattedEdits, FormattingOptions options, int hostDocumentIndex, char triggerCharacter, CancellationToken cancellationToken)
            {
                return Task.FromResult(formattedEdits);
            }

            public override Task<TextEdit[]> FormatSnippetAsync(Uri uri, DocumentSnapshot documentSnapshot, RazorLanguageKind kind, TextEdit[] formattedEdits, FormattingOptions options, CancellationToken cancellationToken)
            {
                return Task.FromResult(formattedEdits);
            }

            public override Task<OmniSharp.Extensions.LanguageServer.Protocol.Models.TextEdit[]> OmniFormatOnTypeAsync(Uri uri, DocumentSnapshot documentSnapshot, RazorLanguageKind kind, OmniSharp.Extensions.LanguageServer.Protocol.Models.TextEdit[] formattedEdits, OmniSharp.Extensions.LanguageServer.Protocol.Models.FormattingOptions options, int hostDocumentIndex, char triggerCharacter, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
