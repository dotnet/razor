﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Moq;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    public class DefaultCSharpCodeActionResolverTest : LanguageServerTestBase
    {
        private static readonly CodeAction s_defaultResolvedCodeAction = new CodeAction()
        {
            Title = "ResolvedCodeAction",
            Data = JToken.FromObject(new object()),
            Edit = new WorkspaceEdit()
            {
                DocumentChanges = new Container<WorkspaceEditDocumentChange>(
                    new WorkspaceEditDocumentChange(
                        new TextDocumentEdit()
                        {
                            Edits = new TextEditContainer(
                                new TextEdit()
                                {
                                    NewText = "Generated C# Based Edit"
                                }
                            )
                        }
                    ))
            }
        };

        private static readonly TextEdit[] s_defaultFormattedEdits = new TextEdit[]
        {
            new TextEdit()
            {
                NewText = "Remapped & Formatted Edit"
            }
        };

        private static readonly CodeAction s_defaultUnresolvedCodeAction = new CodeAction()
        {
            Title = "Unresolved Code Action"
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
            var returnedEdits = Assert.Single(returnedCodeAction.Edit.DocumentChanges);
            Assert.True(returnedEdits.IsTextDocumentEdit);
            var returnedTextDocumentEdit = Assert.Single(returnedEdits.TextDocumentEdit.Edits);
            Assert.Equal(s_defaultFormattedEdits.First(), returnedTextDocumentEdit);
        }

        [Fact]
        public async Task ResolveAsync_NoDocumentChanges_ReturnsOriginalCodeAction()
        {
            // Arrange
            var resolvedCodeAction = new CodeAction()
            {
                Title = "ResolvedCodeAction",
                Data = JToken.FromObject(new object()),
                Edit = new WorkspaceEdit()
                {
                    DocumentChanges = null
                }
            };

            var languageServer = CreateLanguageServer(resolvedCodeAction);

            CreateCodeActionResolver(out var codeActionParams, out var csharpCodeActionResolver, languageServer: languageServer);

            // Act
            var returnedCodeAction = await csharpCodeActionResolver.ResolveAsync(codeActionParams, s_defaultUnresolvedCodeAction, default);

            // Assert
            Assert.Equal(s_defaultUnresolvedCodeAction.Title, returnedCodeAction.Title);
        }

        [Fact]
        public async Task ResolveAsync_MultipleDocumentChanges_ReturnsOriginalCodeAction()
        {
            // Arrange
            var resolvedCodeAction = new CodeAction()
            {
                Title = "ResolvedCodeAction",
                Data = JToken.FromObject(new object()),
                Edit = new WorkspaceEdit()
                {
                    DocumentChanges = new Container<WorkspaceEditDocumentChange>(
                        new WorkspaceEditDocumentChange(
                            new TextDocumentEdit()
                            {
                                Edits = new TextEditContainer(
                                    new TextEdit()
                                    {
                                        NewText = "1. Generated C# Based Edit"
                                    }
                                )
                            }
                        ),
                        new WorkspaceEditDocumentChange(
                            new TextDocumentEdit()
                            {
                                Edits = new TextEditContainer(
                                    new TextEdit()
                                    {
                                        NewText = "2. Generated C# Based Edit"
                                    }
                                )
                            }
                        ))
                }
            };

            var languageServer = CreateLanguageServer(resolvedCodeAction);

            CreateCodeActionResolver(out var codeActionParams, out var csharpCodeActionResolver, languageServer: languageServer);

            // Act
            var returnedCodeAction = await csharpCodeActionResolver.ResolveAsync(codeActionParams, s_defaultUnresolvedCodeAction, default);

            // Assert
            Assert.Equal(s_defaultUnresolvedCodeAction.Title, returnedCodeAction.Title);
        }

        [Fact]
        public async Task ResolveAsync_NonTextDocumentEdit_ReturnsOriginalCodeAction()
        {
            // Arrange
            var resolvedCodeAction = new CodeAction()
            {
                Title = "ResolvedCodeAction",
                Data = JToken.FromObject(new object()),
                Edit = new WorkspaceEdit()
                {
                    DocumentChanges = new Container<WorkspaceEditDocumentChange>(
                        new WorkspaceEditDocumentChange(
                            new CreateFile()
                            {
                                Uri = new Uri("c:/some/uri.razor")
                            }
                        ))
                }
            };

            var languageServer = CreateLanguageServer(resolvedCodeAction);

            CreateCodeActionResolver(out var codeActionParams, out var csharpCodeActionResolver, languageServer: languageServer);

            // Act
            var returnedCodeAction = await csharpCodeActionResolver.ResolveAsync(codeActionParams, s_defaultUnresolvedCodeAction, default);

            // Assert
            Assert.Equal(s_defaultUnresolvedCodeAction.Title, returnedCodeAction.Title);
        }

        private void CreateCodeActionResolver(
            out CSharpCodeActionParams codeActionParams,
            out DefaultCSharpCodeActionResolver csharpCodeActionResolver,
            ClientNotifierServiceBase languageServer = null,
            DocumentVersionCache documentVersionCache = null,
            RazorFormattingService razorFormattingService = null)
        {
            var documentPath = "c:/Test.razor";
            var documentUri = new Uri(documentPath);
            var contents = string.Empty;
            var codeDocument = CreateCodeDocument(contents, documentPath);

            codeActionParams = new CSharpCodeActionParams()
            {
                Data = new JObject(),
                RazorFileUri = documentUri
            };

            languageServer ??= CreateLanguageServer();
            documentVersionCache ??= CreateDocumentVersionCache();
            razorFormattingService ??= CreateRazorFormattingService(documentUri);

            csharpCodeActionResolver = new DefaultCSharpCodeActionResolver(
                LegacyDispatcher,
                CreateDocumentResolver(documentPath, codeDocument),
                languageServer,
                razorFormattingService,
                documentVersionCache);
        }

        private static RazorFormattingService CreateRazorFormattingService(Uri documentUri)
        {
            var razorFormattingService = Mock.Of<RazorFormattingService>(
                            rfs => rfs.ApplyFormattedEditsAsync(
                                documentUri,
                                It.IsAny<DocumentSnapshot>(),
                                RazorLanguageKind.CSharp,
                                It.IsAny<TextEdit[]>(),
                                It.IsAny<FormattingOptions>(),
                                It.IsAny<CancellationToken>(),
                                /*bypassValidationPasses:*/ true,
                                It.IsAny<bool>()) == Task.FromResult(s_defaultFormattedEdits), MockBehavior.Strict);
            return razorFormattingService;
        }

        private static DocumentVersionCache CreateDocumentVersionCache()
        {
            int? documentVersion = 2;
            var documentVersionCache = Mock.Of<DocumentVersionCache>(dvc => dvc.TryGetDocumentVersion(It.IsAny<DocumentSnapshot>(), out documentVersion) == true, MockBehavior.Strict);
            return documentVersionCache;
        }

        private static ClientNotifierServiceBase CreateLanguageServer(CodeAction resolvedCodeAction = null)
        {
            var responseRouterReturns = new Mock<IResponseRouterReturns>(MockBehavior.Strict);
            responseRouterReturns
                .Setup(l => l.Returning<CodeAction>(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(resolvedCodeAction ?? s_defaultResolvedCodeAction));

            var languageServer = new Mock<ClientNotifierServiceBase>(MockBehavior.Strict);
            languageServer
                .Setup(l => l.SendRequestAsync(LanguageServerConstants.RazorResolveCodeActionsEndpoint, It.IsAny<CodeAction>()))
                .Returns(Task.FromResult(responseRouterReturns.Object));

            return languageServer.Object;
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
            return documentResolver.Object;
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
