// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.AutoInsert;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Test;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    public class OnAutoInsertEndpointTest : SingleServerDelegatingEndpointTestBase
    {
        public OnAutoInsertEndpointTest()
        {
            EmptyDocumentContextFactory = Mock.Of<DocumentContextFactory>(r => r.TryCreateAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()) == Task.FromResult<DocumentContext?>(null), MockBehavior.Strict);
        }

        private DocumentContextFactory EmptyDocumentContextFactory { get; }

        [Fact]
        public async Task Handle_SingleProvider_InvokesProvider()
        {
            // Arrange
            var codeDocument = CreateCodeDocument();
            var razorFilePath = "file://path/test.razor";
            await CreateLanguageServerAsync(codeDocument, razorFilePath);

            var insertProvider = new TestOnAutoInsertProvider(">", canResolve: true, LoggerFactory);
            var razorFormattingService = Mock.Of<RazorFormattingService>(MockBehavior.Strict);
            var providers = new[] { insertProvider };
            var endpoint = new OnAutoInsertEndpoint(DocumentContextFactory, LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, providers, TestAdhocWorkspaceFactory.Instance, razorFormattingService, LoggerFactory);
            var @params = new OnAutoInsertParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(razorFilePath), },
                Position = new Position(0, 0),
                Character = ">",
                Options = new FormattingOptions
                {
                    TabSize = 4,
                    InsertSpaces = true
                },
            };

            // Act
            var result = await endpoint.Handle(@params, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(insertProvider.Called);
            Assert.Equal(0, LanguageServer.RequestCount);
        }

        [Fact]
        public async Task Handle_MultipleProviderSameTrigger_UsesSuccessful()
        {
            // Arrange
            var codeDocument = CreateCodeDocument();
            var razorFilePath = "file://path/test.razor";
            await CreateLanguageServerAsync(codeDocument, razorFilePath);

            var insertProvider1 = new TestOnAutoInsertProvider(">", canResolve: false, LoggerFactory)
            {
                ResolvedTextEdit = new TextEdit()
            };
            var insertProvider2 = new TestOnAutoInsertProvider(">", canResolve: true, LoggerFactory)
            {
                ResolvedTextEdit = new TextEdit()
            };
            var razorFormattingService = Mock.Of<RazorFormattingService>(MockBehavior.Strict);
            var providers = new[] { insertProvider1, insertProvider2 };
            var endpoint = new OnAutoInsertEndpoint(DocumentContextFactory, LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, providers, TestAdhocWorkspaceFactory.Instance, razorFormattingService, LoggerFactory);
            var @params = new OnAutoInsertParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(razorFilePath), },
                Position = new Position(0, 0),
                Character = ">",
                Options = new FormattingOptions
                {
                    TabSize = 4,
                    InsertSpaces = true
                },
            };

            // Act
            var result = await endpoint.Handle(@params, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(insertProvider1.Called);
            Assert.True(insertProvider2.Called);
            Assert.Same(insertProvider2.ResolvedTextEdit, result?.TextEdit);
            Assert.Equal(0, LanguageServer.RequestCount);
        }

        [Fact]
        public async Task Handle_MultipleProviderSameTrigger_UsesFirstSuccessful()
        {
            // Arrange
            var codeDocument = CreateCodeDocument();
            var razorFilePath = "file://path/test.razor";
            await CreateLanguageServerAsync(codeDocument, razorFilePath);

            var insertProvider1 = new TestOnAutoInsertProvider(">", canResolve: true, LoggerFactory)
            {
                ResolvedTextEdit = new TextEdit()
            };
            var insertProvider2 = new TestOnAutoInsertProvider(">", canResolve: true, LoggerFactory)
            {
                ResolvedTextEdit = new TextEdit()
            };
            var razorFormattingService = Mock.Of<RazorFormattingService>(MockBehavior.Strict);
            var providers = new[] { insertProvider1, insertProvider2 };
            var endpoint = new OnAutoInsertEndpoint(DocumentContextFactory, LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, providers, TestAdhocWorkspaceFactory.Instance, razorFormattingService, LoggerFactory);
            var @params = new OnAutoInsertParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(razorFilePath), },
                Position = new Position(0, 0),
                Character = ">",
                Options = new FormattingOptions
                {
                    TabSize = 4,
                    InsertSpaces = true
                },
            };

            // Act
            var result = await endpoint.Handle(@params, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(insertProvider1.Called);
            Assert.False(insertProvider2.Called);
            Assert.Same(insertProvider1.ResolvedTextEdit, result?.TextEdit);
        }

        [Fact]
        public async Task Handle_MultipleProviderUnmatchingTrigger_ReturnsNull()
        {
            // Arrange
            var codeDocument = CreateCodeDocument();
            var razorFilePath = "file://path/test.razor";
            await CreateLanguageServerAsync(codeDocument, razorFilePath);

            var insertProvider1 = new TestOnAutoInsertProvider(">", canResolve: true, LoggerFactory);
            var insertProvider2 = new TestOnAutoInsertProvider("<", canResolve: true, LoggerFactory);
            var razorFormattingService = Mock.Of<RazorFormattingService>(MockBehavior.Strict);
            var providers = new[] { insertProvider1, insertProvider2 };
            var endpoint = new OnAutoInsertEndpoint(DocumentContextFactory, LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, providers, TestAdhocWorkspaceFactory.Instance, razorFormattingService, LoggerFactory);
            var @params = new OnAutoInsertParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(razorFilePath), },
                Position = new Position(0, 0),
                Character = "!",
                Options = new FormattingOptions
                {
                    TabSize = 4,
                    InsertSpaces = true
                },
            };

            // Act
            var result = await endpoint.Handle(@params, CancellationToken.None);

            // Assert
            Assert.Null(result);
            Assert.False(insertProvider1.Called);
            Assert.False(insertProvider2.Called);
            Assert.Equal(0, LanguageServer.RequestCount);
        }

        [Fact]
        public async Task Handle_DocumentNotFound_ReturnsNull()
        {
            // Arrange
            var codeDocument = CreateCodeDocument();
            var razorFilePath = "file://path/test.razor";
            await CreateLanguageServerAsync(codeDocument, razorFilePath);

            var insertProvider = new TestOnAutoInsertProvider(">", canResolve: true, LoggerFactory);
            var razorFormattingService = Mock.Of<RazorFormattingService>(MockBehavior.Strict);
            var providers = new[] { insertProvider };
            var endpoint = new OnAutoInsertEndpoint(EmptyDocumentContextFactory, LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, providers, TestAdhocWorkspaceFactory.Instance, razorFormattingService, LoggerFactory);
            var @params = new OnAutoInsertParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(razorFilePath), },
                Position = new Position(0, 0),
                Character = ">",
                Options = new FormattingOptions
                {
                    TabSize = 4,
                    InsertSpaces = true
                },
            };

            // Act
            var result = await endpoint.Handle(@params, CancellationToken.None);

            // Assert
            Assert.Null(result);
            Assert.False(insertProvider.Called);
            Assert.Equal(0, LanguageServer.RequestCount);
        }

        [Fact]
        public async Task Handle_UnsupportedCodeDocument_ReturnsNull()
        {
            // Arrange
            var codeDocument = CreateCodeDocument();
            codeDocument.SetUnsupported();
            var razorFilePath = "file://path/test.razor";
            await CreateLanguageServerAsync(codeDocument, razorFilePath);

            var insertProvider = new TestOnAutoInsertProvider(">", canResolve: true, LoggerFactory);
            var razorFormattingService = Mock.Of<RazorFormattingService>(MockBehavior.Strict);
            var providers = new[] { insertProvider };
            var endpoint = new OnAutoInsertEndpoint(DocumentContextFactory, LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, providers, TestAdhocWorkspaceFactory.Instance, razorFormattingService, LoggerFactory);
            var @params = new OnAutoInsertParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(razorFilePath), },
                Position = new Position(0, 0),
                Character = ">",
                Options = new FormattingOptions
                {
                    TabSize = 4,
                    InsertSpaces = true
                },
            };

            // Act
            var result = await endpoint.Handle(@params, CancellationToken.None);

            // Assert
            Assert.Null(result);
            Assert.False(insertProvider.Called);
            Assert.Equal(0, LanguageServer.RequestCount);
        }

        [Fact]
        public async Task Handle_NoApplicableProvider_CallsProviderAndReturnsNull()
        {
            // Arrange
            var codeDocument = CreateCodeDocument();
            var razorFilePath = "file://path/test.razor";
            await CreateLanguageServerAsync(codeDocument, razorFilePath);

            var insertProvider = new TestOnAutoInsertProvider(">", canResolve: false, LoggerFactory);
            var razorFormattingService = Mock.Of<RazorFormattingService>(MockBehavior.Strict);
            var providers = new[] { insertProvider };
            var endpoint = new OnAutoInsertEndpoint(DocumentContextFactory, LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, providers, TestAdhocWorkspaceFactory.Instance, razorFormattingService, LoggerFactory);
            var @params = new OnAutoInsertParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(razorFilePath), },
                Position = new Position(0, 0),
                Character = ">",
                Options = new FormattingOptions
                {
                    TabSize = 4,
                    InsertSpaces = true
                },
            };

            // Act
            var result = await endpoint.Handle(@params, CancellationToken.None);

            // Assert
            Assert.Null(result);
            Assert.True(insertProvider.Called);
            Assert.Equal(0, LanguageServer.RequestCount);
        }

        [Fact]
        public async Task Handle_SingleServer_CSharpDocCommentSnippet()
        {
            // Arrange
            var input = """
                <div>
                </div>

                @functions {
                    ///$$
                    public void M()
                    {
                    }
                }
                """;

            var expected = """
                <div>
                </div>

                @functions {
                    /// <summary>
                    /// $0
                    /// </summary>
                    public void M()
                    {
                    }
                }
                """;

            var character = "/";

            await VerifyCSharpOnAutoInsertAsync(input, expected, character);
        }

        [Fact]
        public async Task Handle_SingleServer_CSharpDocCommentNewLine()
        {
            // Arrange
            var input = """
                <div>
                </div>

                @functions {
                    /// <summary>
                    /// This is some text
                    $$
                    /// </summary>
                    public void M()
                    {
                    }
                }
                """;

            var expected = """
                <div>
                </div>

                @functions {
                    /// <summary>
                    /// This is some text
                    /// $0
                    /// </summary>
                    public void M()
                    {
                    }
                }
                """;

            var character = "\n";

            await VerifyCSharpOnAutoInsertAsync(input, expected, character);
        }

        [Fact(Skip = "Roslyn only responds to the Razor server kind for this request, but uses the C# server kind in tests")]
        public async Task Handle_SingleServer_CSharpBraceMatching()
        {
            // Arrange
            var input = """
                <div>
                </div>

                @functions {
                    public void M()
                    {
                    $$}
                }
                """;

            var expected = """
                <div>
                </div>

                @functions {
                    public void M()
                    {
                        $0
                    }
                }
                """;

            var character = "\n";

            await VerifyCSharpOnAutoInsertAsync(input, expected, character);
        }

        private async Task VerifyCSharpOnAutoInsertAsync(string input, string expected, string character)
        {
            TestFileMarkupParser.GetPosition(input, out input, out var cursorPosition);

            var codeDocument = CreateCodeDocument(input);
            var razorFilePath = "file://path/test.razor";
            await CreateLanguageServerAsync(codeDocument, razorFilePath);

            var insertProvider = new TestOnAutoInsertProvider("!!!", canResolve: false, LoggerFactory);
            var razorFormattingService = TestRazorFormattingService.Instance;
            var providers = new[] { insertProvider };
            var endpoint = new OnAutoInsertEndpoint(DocumentContextFactory, LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, providers, TestAdhocWorkspaceFactory.Instance, razorFormattingService, LoggerFactory);

            codeDocument.GetSourceText().GetLineAndOffset(cursorPosition, out var line, out var offset);
            var @params = new OnAutoInsertParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(razorFilePath), },
                Position = new Position(line, offset),
                Character = character,
                Options = new FormattingOptions
                {
                    TabSize = 4,
                    InsertSpaces = true
                },
            };

            // Act
            var result = await endpoint.Handle(@params, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.False(insertProvider.Called);
            Assert.Equal(1, LanguageServer.RequestCount);

            var edits = new[] { result!.TextEdit.AsTextChange(codeDocument.GetSourceText()) };
            var newText = codeDocument.GetSourceText().WithChanges(edits).ToString();
            Assert.Equal(expected, newText);
        }

        private class TestOnAutoInsertProvider : RazorOnAutoInsertProvider
        {
            private readonly bool _canResolve;

            public TestOnAutoInsertProvider(string triggerCharacter, bool canResolve, ILoggerFactory loggerFactory) : base(loggerFactory)
            {
                TriggerCharacter = triggerCharacter;
                _canResolve = canResolve;
            }

            public bool Called { get; private set; }

            public TextEdit? ResolvedTextEdit { get; set; }

            public override string TriggerCharacter { get; }

            // Disabling because [NotNullWhen] is available in two Assemblies and causes warnings
#pragma warning disable CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
            public override bool TryResolveInsertion(Position position, FormattingContext context, out TextEdit? edit, out InsertTextFormat format)
#pragma warning restore CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
            {
                Called = true;
                edit = ResolvedTextEdit!;
                format = default;
                return _canResolve;
            }
        }

        private static RazorCodeDocument CreateCodeDocument()
        {
            return CreateCodeDocument("");
        }
    }
}
