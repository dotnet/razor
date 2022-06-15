// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    public class AddUsingsCSharpCodeActionResolverTest : LanguageServerTestBase
    {
        private static readonly CodeAction s_defaultResolvedCodeAction = new CodeAction()
        {
            Title = "@using System.Net",
            Data = null,
            Edit = new WorkspaceEdit()
            {
                DocumentChanges = new TextDocumentEdit[] {
                    new TextDocumentEdit()
                    {
                        Edits = new TextEdit[] {
                            new TextEdit()
                            {
                                NewText = "using System.Net;"
                            }
                        }
                    }
                }
            }
        };

        private static readonly CodeAction s_defaultUnresolvedCodeAction = new CodeAction()
        {
            Title = "@using System.Net"
        };

        [Fact]
        public async Task ResolveAsync_ReturnsResolvedCodeAction()
        {
            // Arrange
            CreateCodeActionResolver(out var codeActionParams, out var csharpCodeActionResolver);

            // Act
            var returnedCodeAction = await csharpCodeActionResolver.ResolveAsync(codeActionParams, s_defaultUnresolvedCodeAction, default);

            // Assert
            Assert.Equal(s_defaultResolvedCodeAction.Title, returnedCodeAction.Title);
            Assert.Equal(s_defaultResolvedCodeAction.Data, returnedCodeAction.Data);

            Assert.Equal(1, returnedCodeAction.Edit.DocumentChanges.Value.Count());
            var returnedEdits = returnedCodeAction.Edit.DocumentChanges.Value.First();
            Assert.True(returnedEdits.TryGetFirst(out var textDocumentEdit));
            var returnedTextDocumentEdit = Assert.Single(textDocumentEdit.Edits);
            Assert.Equal($"@using System.Net{Environment.NewLine}", returnedTextDocumentEdit.NewText);
        }

        private void CreateCodeActionResolver(
            out CSharpCodeActionParams codeActionParams,
            out AddUsingsCSharpCodeActionResolver addUsingResolver)
        {
            var documentUri = new Uri("c:/Test.razor");
            var contents = string.Empty;
            var codeDocument = CreateCodeDocument(contents, documentUri.AbsolutePath);

            codeActionParams = new CSharpCodeActionParams()
            {
                Data = new JObject(),
                RazorFileUri = documentUri
            };

            var languageServer = CreateLanguageServer();

            addUsingResolver = new AddUsingsCSharpCodeActionResolver(
                CreateDocumentContextFactory(documentUri, codeDocument),
                languageServer);
        }

        private static ClientNotifierServiceBase CreateLanguageServer()
        {
            var languageServer = new Mock<ClientNotifierServiceBase>(MockBehavior.Strict);

            return languageServer.Object;
        }

        private static RazorCodeDocument CreateCodeDocument(string text, string documentPath)
        {
            var projectItem = new TestRazorProjectItem(documentPath) { Content = text };
            var projectEngine = RazorProjectEngine.Create(RazorConfiguration.Default, TestRazorProjectFileSystem.Empty, (builder) => PageDirective.Register(builder));
            var codeDocument = projectEngine.Process(projectItem);
            codeDocument.SetFileKind(FileKinds.Component);
            return codeDocument;
        }
    }
}
