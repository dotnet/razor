// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Microsoft.AspNetCore.Razor.LanguageServer.LinkedEditingRange;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Moq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.LinkedEditingRange
{
    public class LinkedEditingRangeEndpointTest : TagHelperServiceTestBase
    {
        [Fact]
        public async Task Handle_StartTag_ReturnsCorrectRange()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1></test1>";
            var codeDocument = CreateCodeDocument(txt, DefaultTagHelpers);
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var endpoint = new LinkedEditingRangeEndpoint(Dispatcher, documentResolver, TagHelperFactsService);
            var request = new LinkedEditingRangeParams
            {
                TextDocument = new TextDocumentIdentifier(uri),
                Position = new Position { Line = 1, Character = 3 } // <te[||]st1></test1>
            };

            var expectedRanges = new Range[]
            {
                new Range
                {
                    Start = new Position { Line = 1, Character = 1 },
                    End = new Position { Line = 1, Character = 6 }
                },
                new Range
                {
                    Start = new Position { Line = 1, Character = 9 },
                    End = new Position { Line = 1, Character = 14 }
                }
            };

            // Act
            var result = await endpoint.Handle(request, CancellationToken.None);

            // Assert
            Assert.Equal(expectedRanges, result.Ranges);
            Assert.Equal(endpoint._wordPattern, result.WordPattern);
        }

        [Fact]
        public async Task Handle_EndTag_ReturnsCorrectRange()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1></test1>";
            var codeDocument = CreateCodeDocument(txt, DefaultTagHelpers);
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var endpoint = new LinkedEditingRangeEndpoint(Dispatcher, documentResolver, TagHelperFactsService);
            var request = new LinkedEditingRangeParams
            {
                TextDocument = new TextDocumentIdentifier(uri),
                Position = new Position { Line = 1, Character = 9 } // <test1></[||]test1>
            };

            var expectedRanges = new Range[]
            {
                new Range
                {
                    Start = new Position { Line = 1, Character = 1 },
                    End = new Position { Line = 1, Character = 6 }
                },
                new Range
                {
                    Start = new Position { Line = 1, Character = 9 },
                    End = new Position { Line = 1, Character = 14 }
                }
            };

            // Act
            var result = await endpoint.Handle(request, CancellationToken.None);

            // Assert
            Assert.Equal(expectedRanges, result.Ranges);
            Assert.Equal(endpoint._wordPattern, result.WordPattern);
        }

        [Fact]
        public async Task Handle_NoTagHelper_ReturnsNull()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1></test1>";
            var codeDocument = CreateCodeDocument(txt, DefaultTagHelpers);
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var endpoint = new LinkedEditingRangeEndpoint(Dispatcher, documentResolver, TagHelperFactsService);
            var request = new LinkedEditingRangeParams
            {
                TextDocument = new TextDocumentIdentifier(uri),
                Position = new Position { Line = 0, Character = 1 } // @[||]addTagHelper *
            };

            // Act
            var result = await endpoint.Handle(request, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_SelfClosingTagHelper_ReturnsNull()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 />";
            var codeDocument = CreateCodeDocument(txt, DefaultTagHelpers);
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var endpoint = new LinkedEditingRangeEndpoint(Dispatcher, documentResolver, TagHelperFactsService);
            var request = new LinkedEditingRangeParams
            {
                TextDocument = new TextDocumentIdentifier(uri),
                Position = new Position { Line = 1, Character = 3 } // <te[||]st1 />
            };

            // Act
            var result = await endpoint.Handle(request, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_NestedStartTags_ReturnsCorrectRange()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1><test1></test1></test1>";
            var codeDocument = CreateCodeDocument(txt, DefaultTagHelpers);
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var endpoint = new LinkedEditingRangeEndpoint(Dispatcher, documentResolver, TagHelperFactsService);
            var request = new LinkedEditingRangeParams
            {
                TextDocument = new TextDocumentIdentifier(uri),
                Position = new Position { Line = 1, Character = 1 } // <[||]test1><test1></test1></test1>
            };

            var expectedRanges = new Range[]
            {
                new Range
                {
                    Start = new Position { Line = 1, Character = 1 },
                    End = new Position { Line = 1, Character = 6 }
                },
                new Range
                {
                    Start = new Position { Line = 1, Character = 24 },
                    End = new Position { Line = 1, Character = 29 }
                }
            };

            // Act
            var result = await endpoint.Handle(request, CancellationToken.None);

            // Assert
            Assert.Equal(expectedRanges, result.Ranges);
            Assert.Equal(endpoint._wordPattern, result.WordPattern);
        }

        [Fact]
        public void VerifyWordPatternCorrect()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1></test1>";
            var codeDocument = CreateCodeDocument(txt, DefaultTagHelpers);
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var endpoint = new LinkedEditingRangeEndpoint(Dispatcher, documentResolver, TagHelperFactsService);

            // Assert
            Assert.True(Regex.Match("Test", endpoint._wordPattern).Length == 4);
            Assert.True(Regex.Match("!Test", endpoint._wordPattern).Length == 5);
            Assert.True(Regex.Match("!Test.Test2", endpoint._wordPattern).Length == 11);

            Assert.True(Regex.Match("Te>st", endpoint._wordPattern).Length != 5);
            Assert.True(Regex.Match("Te/st", endpoint._wordPattern).Length != 5);
            Assert.True(Regex.Match("Te\\st", endpoint._wordPattern).Length != 5);
            Assert.True(Regex.Match("Te!st", endpoint._wordPattern).Length != 5);
            Assert.True(Regex.Match("Te" + Environment.NewLine + "st",
                endpoint._wordPattern).Length != 4 + Environment.NewLine.Length);
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
            documentResolver.Setup(resolver => resolver.TryResolveDocument(documentPath, out documentSnapshot))
                .Returns(true);
            return documentResolver.Object;
        }
    }
}
