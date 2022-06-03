﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class RazorDocumentSynchronizationEndpointTest : LanguageServerTestBase
    {
        private static DocumentResolver DocumentResolver => Mock.Of<DocumentResolver>(MockBehavior.Strict);

        private static RazorProjectService ProjectService => Mock.Of<RazorProjectService>(MockBehavior.Strict);

        [Fact]
        public void ApplyContentChanges_SingleChange()
        {
            // Arrange
            var endpoint = new RazorDocumentSynchronizationEndpoint(Dispatcher, DocumentResolver, ProjectService, LoggerFactory);
            var sourceText = SourceText.From("Hello World");
            var change = new TextDocumentContentChangeEvent()
            {
                Range = new Range
                {
                    Start = new Position(0, 5),
                    End = new Position(0, 5),
                },
                RangeLength = 0,
                Text = "!"
            };

            // Act
            var result = endpoint.ApplyContentChanges(new[] { change }, sourceText);

            // Assert
            var resultString = GetString(result);
            Assert.Equal("Hello! World", resultString);
        }

        [Fact]
        public void ApplyContentChanges_MultipleChanges()
        {
            // Arrange
            var endpoint = new RazorDocumentSynchronizationEndpoint(Dispatcher, DocumentResolver, ProjectService, LoggerFactory);
            var sourceText = SourceText.From("Hello World");
            var changes = new[] {
                new TextDocumentContentChangeEvent()
                {
                    Range = new Range{
                        Start = new Position(0, 5),
                        End = new Position(0, 5)
                    },
                    RangeLength = 0,
                    Text = Environment.NewLine
                },
                // Hello
                //  World

                new TextDocumentContentChangeEvent()
                {
                    Range = new Range{
                        Start = new Position(1, 0),
                        End = new Position(1, 0),
                    },
                    RangeLength = 0,
                    Text = "!"
                },
                // Hello
                // ! World

                new TextDocumentContentChangeEvent()
                {
                    Range = new Range{
                        Start = new Position(0, 1),
                        End = new Position(0, 1)
                    },
                    RangeLength = 4,
                    Text = "i!" + Environment.NewLine
                },
                // Hi!
                //
                // ! World
            };

            // Act
            var result = endpoint.ApplyContentChanges(changes, sourceText);

            // Assert
            var resultString = GetString(result);
            Assert.Equal(@"Hi!

! World", resultString);
        }

        // This is more of an integration test to validate that all the pieces work together
        [Fact]
        public async Task Handle_DidChangeTextDocument_UpdatesDocument()
        {
            // Arrange
            var documentPath = "C:/path/to/document.cshtml";
            var sourceText = SourceText.From("<p>");
            var documentResolver = CreateDocumentResolver(documentPath, sourceText);
            var projectService = new Mock<RazorProjectService>(MockBehavior.Strict);
            projectService.Setup(service => service.UpdateDocument(It.IsAny<string>(), It.IsAny<SourceText>(), It.IsAny<int>()))
                .Callback<string, SourceText, int>((path, text, version) =>
                {
                    var resultString = GetString(text);
                    Assert.Equal("<p></p>", resultString);
                    Assert.Equal(documentPath, path);
                    Assert.Equal(1337, version);
                });
            var endpoint = new RazorDocumentSynchronizationEndpoint(Dispatcher, documentResolver, projectService.Object, LoggerFactory);
            var change = new TextDocumentContentChangeEvent()
            {
                Range = new Range
                {
                    Start = new Position(0, 3),
                    End = new Position(0, 3),
                },
                RangeLength = 0,
                Text = "</p>"
            };
            var request = new DidChangeTextDocumentParamsBridge()
            {
                ContentChanges = new TextDocumentContentChangeEvent[] { change },
                TextDocument = new VersionedTextDocumentIdentifier()
                {
                    Uri = new Uri(documentPath),
                    Version = 1337,
                }
            };

            // Act
            await Task.Run(() => endpoint.Handle(request, default));

            // Assert
            projectService.VerifyAll();
        }

        // This is more of an integration test to validate that all the pieces work together
        [Fact]
        public async Task Handle_DidOpenTextDocument_AddsDocument()
        {
            // Arrange
            var documentPath = "C:/path/to/document.cshtml";
            var projectService = new Mock<RazorProjectService>(MockBehavior.Strict);
            projectService.Setup(service => service.OpenDocument(It.IsAny<string>(), It.IsAny<SourceText>(), It.IsAny<int>()))
                .Callback<string, SourceText, int>((path, text, version) =>
                {
                    var resultString = GetString(text);
                    Assert.Equal("hello", resultString);
                    Assert.Equal(documentPath, path);
                    Assert.Equal(1337, version);
                });
            var endpoint = new RazorDocumentSynchronizationEndpoint(Dispatcher, DocumentResolver, projectService.Object, LoggerFactory);
            var request = new DidOpenTextDocumentParamsBridge()
            {
                TextDocument = new TextDocumentItem()
                {
                    Text = "hello",
                    Uri = new Uri(documentPath),
                    Version = 1337,
                }
            };

            // Act
            await Task.Run(() => endpoint.Handle(request, default));

            // Assert
            projectService.VerifyAll();
        }

        // This is more of an integration test to validate that all the pieces work together
        [Fact]
        public async Task Handle_DidCloseTextDocument_ClosesDocument()
        {
            // Arrange
            var documentPath = "C:/path/to/document.cshtml";
            var projectService = new Mock<RazorProjectService>(MockBehavior.Strict);
            projectService.Setup(service => service.CloseDocument(It.IsAny<string>()))
                .Callback<string>((path) => Assert.Equal(documentPath, path));
            var endpoint = new RazorDocumentSynchronizationEndpoint(Dispatcher, DocumentResolver, projectService.Object, LoggerFactory);
            var request = new DidCloseTextDocumentParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = new Uri(documentPath)
                }
            };

            // Act
            await Task.Run(() => endpoint.Handle(request, default));

            // Assert
            projectService.VerifyAll();
        }

        private static string GetString(SourceText sourceText)
        {
            var sourceChars = new char[sourceText.Length];
            sourceText.CopyTo(0, sourceChars, 0, sourceText.Length);
            var sourceString = new string(sourceChars);

            return sourceString;
        }

        private static DocumentResolver CreateDocumentResolver(string documentPath, SourceText sourceText)
        {
            var documentSnapshot = Mock.Of<DocumentSnapshot>(document => document.GetTextAsync() == Task.FromResult(sourceText) && document.FilePath == documentPath, MockBehavior.Strict);
            var documentResolver = new Mock<DocumentResolver>(MockBehavior.Strict);
            documentResolver.Setup(resolver => resolver.TryResolveDocument(documentPath, out documentSnapshot))
                .Returns(true);
            return documentResolver.Object;
        }
    }
}
