// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.JsonRpc;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Moq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using OmniSharpRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Semantic
{
    // Sets the FileName static variable.
    // Finds the test method name using reflection, and uses
    // that to find the expected input/output test files as Embedded resources.
    [IntializeTestFile]
    public class DefaultRazorSemanticTokenInfoServiceTest : SemanticTokenTestBase
    {
        #region CSharp
        [Fact]
        public async Task GetSemanticTokens_CSharp_RazorIfNotReady()
        {
            var txt = $@"<p></p>@{{
    var d = ""t"";
}}";

            var cSharpResponse = new ProvideSemanticTokensResponse(
                tokens: Array.Empty<int>(), isFinalized: true, hostDocumentSyncVersion: 1);
            await AssertSemanticTokensAsync(txt, isRazor: false, csharpTokens: cSharpResponse, documentVersion: 1);
        }

        [Fact]
        public async Task GetSemanticTokens_CSharpBlock_HTML()
        {
            var txt = @$"@{{
    var d = ""t"";
    <p>HTML @d</p>
}}";

            var csharpTokens = new int[]
            {
                20, 4, 3, RazorSemanticTokensLegend.CSharpKeyword, 0, // var
                0, 4, 1, RazorSemanticTokensLegend.CSharpVariable, 0, // d
                0, 2, 1, RazorSemanticTokensLegend.CSharpOperator, 0, // =
                0, 2, 1, RazorSemanticTokensLegend.CSharpPunctuation, 0, // "
                0, 1, 1, RazorSemanticTokensLegend.CSharpString, 0, // t
                0, 1, 1, RazorSemanticTokensLegend.CSharpPunctuation, 0, // "
                0, 1, 1, RazorSemanticTokensLegend.CSharpPunctuation, 0, // ;
                8, 13, 1, RazorSemanticTokensLegend.CSharpVariable, 0, // d
            };

            var cSharpResponse = new ProvideSemanticTokensResponse(
                csharpTokens, isFinalized: true, hostDocumentSyncVersion: 0);
            await AssertSemanticTokensAsync(txt, isRazor: false, csharpTokens: cSharpResponse);
        }

        [Fact]
        public async Task GetSemanticTokens_CSharp_Nested_HTML()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<!--" +
                $"@{{" +
                $"var d = \"string\";" +
                $"@<a></a>" +
                $"}}" +
                $"-->";

            var csharpTokens = new int[]
            {
                29, 6, 3, RazorSemanticTokensLegend.CSharpKeyword, 0, // var
                0, 4, 1, RazorSemanticTokensLegend.CSharpVariable, 0, // d
                0, 2, 1, RazorSemanticTokensLegend.CSharpOperator, 0, // =
                0, 2, 1, RazorSemanticTokensLegend.CSharpPunctuation, 0, // "
                0, 1, 6, RazorSemanticTokensLegend.CSharpString, 0, // string
                0, 6, 1, RazorSemanticTokensLegend.CSharpPunctuation, 0, // "
                0, 1, 1, RazorSemanticTokensLegend.CSharpPunctuation, 0, // ;
            };

            var cSharpResponse = new ProvideSemanticTokensResponse(
                csharpTokens, isFinalized: true, hostDocumentSyncVersion: 0);
            await AssertSemanticTokensAsync(txt, isRazor: false, csharpTokens: cSharpResponse);
        }

        [Fact]
        public async Task GetSemanticTokens_CSharp_VSCodeWorks()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}@{{ var d = }}";

            var cSharpResponse = new ProvideSemanticTokensResponse(
                tokens: Array.Empty<int>(), isFinalized: true, hostDocumentSyncVersion: null);

            await AssertSemanticTokensAsync(txt, isRazor: false, csharpTokens: cSharpResponse, documentVersion: 1);
        }

        [Fact]
        public async Task GetSemanticTokens_CSharp_Explicit()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}@(DateTime.Now)";

            var csharpTokens = new int[]
            {
                29, 6, 8, RazorSemanticTokensLegend.CSharpVariable, 0, // DateTime
                0, 8, 1, RazorSemanticTokensLegend.CSharpPunctuation, 0, // .
                0, 1, 3, RazorSemanticTokensLegend.CSharpVariable, 0, // Now
            };

            var cSharpResponse = new ProvideSemanticTokensResponse(csharpTokens, isFinalized: true, hostDocumentSyncVersion: 0);
            await AssertSemanticTokensAsync(txt, isRazor: false, csharpTokens: cSharpResponse);
        }

        [Fact]
        public async Task GetSemanticTokens_CSharp_Implicit()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}@{{ var d = \"txt\";}}{Environment.NewLine}" +
                $"@d";

            var csharpTokens = new int[]
            {
                29, 3, 3, RazorSemanticTokensLegend.CSharpKeyword, 0, // var
                0, 4, 1, RazorSemanticTokensLegend.CSharpVariable, 0, // d
                0, 2, 1, RazorSemanticTokensLegend.CSharpOperator, 0, // =
                0, 2, 1, RazorSemanticTokensLegend.CSharpOperator, 0, // "
                0, 1, 3, RazorSemanticTokensLegend.CSharpString, 0, // txt
                0, 3, 1, RazorSemanticTokensLegend.CSharpOperator, 0, // "
                0, 1, 1, RazorSemanticTokensLegend.CSharpOperator, 0, // ;
                7, 6, 1, RazorSemanticTokensLegend.CSharpVariable, 0, // d
            };

            var cSharpResponse = new ProvideSemanticTokensResponse(csharpTokens, isFinalized: true, hostDocumentSyncVersion: 0);
            await AssertSemanticTokensAsync(txt, isRazor: false, csharpTokens: cSharpResponse);
        }

        [Fact]
        public async Task GetSemanticTokens_CSharp_VersionMismatch()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}@{{ var d = }}";

            var csharpTokens = new int[]
            {
                29, 3, 3, RazorSemanticTokensLegend.CSharpKeyword, 0, // var
                0, 4, 1, RazorSemanticTokensLegend.CSharpVariable, 0, // d
                0, 2, 1, RazorSemanticTokensLegend.CSharpOperator, 0, // =
                11, 10, 25, RazorSemanticTokensLegend.CSharpKeyword, 0, // No mapping
            };

            var cSharpResponse = new ProvideSemanticTokensResponse(csharpTokens, isFinalized: true, hostDocumentSyncVersion: 42);
            await AssertSemanticTokensAsync(txt, isRazor: false, csharpTokens: cSharpResponse, documentVersion: 21);
        }

        [Fact]
        public async Task GetSemanticTokens_CSharp_FunctionAsync()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}@{{ var d = }}";

            var csharpTokens = new int[]
            {
                29, 3, 3, RazorSemanticTokensLegend.CSharpKeyword, 0, // var
                0, 4, 1, RazorSemanticTokensLegend.CSharpVariable, 0, // d
                0, 2, 1, RazorSemanticTokensLegend.CSharpOperator, 0, // =
                4, 5, 1, RazorSemanticTokensLegend.CSharpKeyword, 0, // No mapping
            };

            var cSharpResponse = new ProvideSemanticTokensResponse(csharpTokens, isFinalized: true, hostDocumentSyncVersion: 0);
            await AssertSemanticTokensAsync(txt, isRazor: false, csharpTokens: cSharpResponse);
        }

        [Fact]
        public async Task GetSemanticTokens_CSharp_StaticModifierAsync()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}@{{ var d = }}";

            var csharpTokens = new int[]
            {
                29, 3, 3, RazorSemanticTokensLegend.CSharpKeyword, 0, // var
                0, 4, 1, RazorSemanticTokensLegend.CSharpVariable, 1, // d
                0, 2, 1, RazorSemanticTokensLegend.CSharpOperator, 0, // =
                4, 5, 1, RazorSemanticTokensLegend.CSharpKeyword, 0, // No mapping
            };

            var cSharpResponse = new ProvideSemanticTokensResponse(csharpTokens, isFinalized: true, hostDocumentSyncVersion: 0);
            await AssertSemanticTokensAsync(txt, isRazor: false, csharpTokens: cSharpResponse);
        }
        #endregion

        #region HTML
        [Fact]
        public async Task GetSemanticTokens_MultipleBlankLines()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}<p>first{Environment.NewLine}second</p>";

            await AssertSemanticTokensAsync(txt, isRazor: false);
        }

        [Fact]
        public async Task GetSemanticTokens_IncompleteTag()
        {
            var txt = "<str class='";

            await AssertSemanticTokensAsync(txt, isRazor: false);
        }

        [Fact]
        public async Task GetSemanticTokens_MinimizedHTMLAttribute()
        {
            var txt = "<p attr />";

            await AssertSemanticTokensAsync(txt, isRazor: false);
        }

        [Fact]
        public async Task GetSemanticTokens_MinimizedHTMLAsync()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<input/> ";

            await AssertSemanticTokensAsync(txt, isRazor: false);
        }

        [Fact]
        public async Task GetSemanticTokens_HTMLCommentAsync()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<!-- comment with comma's --> ";

            await AssertSemanticTokensAsync(txt, isRazor: false);
        }

        [Fact]
        public async Task GetSemanticTokens_PartialHTMLCommentAsync()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<!-- comment";

            await AssertSemanticTokensAsync(txt, isRazor: true);
        }

        [Fact]
        public async Task GetSemanticTokens_HTMLIncludesBang()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<!input/>";

            await AssertSemanticTokensAsync(txt, isRazor: false);
        }
        #endregion

        #region TagHelpers
        [Fact]
        public async Task GetSemanticTokens_HalfOfCommentAsync()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}@* comment";

            await AssertSemanticTokensAsync(txt, isRazor: true);
        }

        [Fact]
        public async Task GetSemanticTokens_NoAttributesAsync()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1></test1> ";

            await AssertSemanticTokensAsync(txt, isRazor: false);
        }

        [Fact]
        public async Task GetSemanticTokens_WithAttributeAsync()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 bool-val='true'></test1> ";

            await AssertSemanticTokensAsync(txt, isRazor: false);
        }

        [Fact]
        public async Task GetSemanticTokens_MinimizedAttribute_BoundAsync()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 bool-val></test1> ";

            await AssertSemanticTokensAsync(txt, isRazor: false);
        }

        [Fact]
        public async Task GetSemanticTokens_MinimizedAttribute_NotBoundAsync()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 notbound></test1> ";

            await AssertSemanticTokensAsync(txt, isRazor: false);
        }

        [Fact]
        public async Task GetSemanticTokens_IgnoresNonTagHelperAttributesAsync()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 bool-val='true' class='display:none'></test1> ";

            await AssertSemanticTokensAsync(txt, isRazor: false);
        }

        [Fact]
        public async Task GetSemanticTokens_TagHelpersNotAvailableInRazorAsync()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 bool-val='true' class='display:none'></test1> ";

            await AssertSemanticTokensAsync(txt, isRazor: true);
        }

        [Fact]
        public async Task GetSemanticTokens_DoesNotApplyOnNonTagHelpersAsync()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<p bool-val='true'></p> ";

            await AssertSemanticTokensAsync(txt, isRazor: false);
        }
        #endregion TagHelpers

        #region DirectiveAttributes
        [Fact]
        public async Task GetSemanticTokens_Razor_MinimizedDirectiveAttributeParameters()
        {
            // Capitalized, non-well-known-HTML elements should not be marked as TagHelpers
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<NotATagHelp @minimized:something /> ";

            await AssertSemanticTokensAsync(txt, isRazor: true);
        }

        [Fact]
        public async Task GetSemanticTokens_Razor_ComponentAttributeAsync()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<Component1 bool-val=\"true\"></Component1>";

            await AssertSemanticTokensAsync(txt, isRazor: true);
        }

        [Fact]
        public async Task GetSemanticTokens_Razor_DirectiveAttributesParametersAsync()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<Component1 @test:something='Function'></Component1> ";

            await AssertSemanticTokensAsync(txt, isRazor: true);
        }

        [Fact]
        public async Task GetSemanticTokens_Razor_NonComponentsDoNotShowInRazorAsync()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 bool-val='true'></test1> ";

            await AssertSemanticTokensAsync(txt, isRazor: true);
        }

        [Fact]
        public async Task GetSemanticTokens_Razor_DirectivesAsync()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<Component1 @test='Function'></Component1> ";

            await AssertSemanticTokensAsync(txt, isRazor: true);
        }

        [Fact]
        public async Task GetSemanticTokens_HandleTransitionEscape()
        {
            var txt = $"@@text";

            await AssertSemanticTokensAsync(txt, isRazor: true);
        }

        [Fact]
        public async Task GetSemanticTokens_Razor_DoNotColorNonTagHelpersAsync()
        {
            var txt = $"{Environment.NewLine}<p @test='Function'></p> ";

            await AssertSemanticTokensAsync(txt, isRazor: true);
        }

        [Fact]
        public async Task GetSemanticTokens_Razor_DoesNotApplyOnNonTagHelpersAsync()
        {
            var txt = $"@addTagHelpers *, TestAssembly{Environment.NewLine}<p></p> ";

            await AssertSemanticTokensAsync(txt, isRazor: true);
        }

        [Fact(Skip = "https://github.com/dotnet/razor-tooling/issues/5948")]
        public async Task GetSemanticTokens_Razor_InRangeAsync()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1></test1> ";

            var startIndex = txt.IndexOf("test1", StringComparison.Ordinal); ;
            var endIndex = startIndex + 5;

            var codeDocument = CreateCodeDocument(txt, DefaultTagHelpers);

            codeDocument.GetSourceText().GetLineAndOffset(startIndex, out var startLine, out var startChar);
            codeDocument.GetSourceText().GetLineAndOffset(endIndex, out var endLine, out var endChar);

            var startPosition = new Position(startLine, startChar);
            var endPosition = new Position(endLine, endChar);
            var location = new OmniSharpRange(startPosition, endPosition);

            await AssertSemanticTokensAsync(txt, isRazor: false, range: location);
        }
        #endregion DirectiveAttributes

        #region Directive
        [Fact]
        public async Task GetSemanticTokens_Razor_CodeDirectiveAsync()
        {
            var txt = $"@code {{}}";

            await AssertSemanticTokensAsync(txt, isRazor: true);
        }

        [Fact]
        public async Task GetSemanticTokens_Razor_CodeDirectiveBodyAsync()
        {
            var txt = @$"@code {{
    public void SomeMethod()
    {{
@DateTime.Now
    }}
}}";

            await AssertSemanticTokensAsync(txt, isRazor: true);
        }

        [Fact]
        public async Task GetSemanticTokens_Razor_UsingDirective()
        {
            var txt = $"@using Microsoft.AspNetCore.Razor";

            await AssertSemanticTokensAsync(txt, isRazor: true);
        }

        [Fact]
        public async Task GetSemanticTokens_Razor_FunctionsDirectiveAsync()
        {
            var txt = $"@functions {{}}";

            await AssertSemanticTokensAsync(txt, isRazor: true);
        }

        [Fact]
        public async Task GetSemanticTokens_Razor_NestedTextDirectives()
        {
            var txt = @$"@functions {{
                private void BidsByShipment(string generatedId, int bids)
                {{
                    if (bids > 0)
                    {{
                        <a class=""Thing"">
                            @if(bids > 0)
                            {{
                                <text>@DateTime.Now</text>
                            }}
                        </a>
                    }}
                }}";

            await AssertSemanticTokensAsync(txt, isRazor: false);
        }

        [Fact]
        public async Task GetSemanticTokens_Razor_NestedTransitions()
        {
            var txt = @$"@functions {{
                Action<object> abc = @<span></span>;
            }}";

            await AssertSemanticTokensAsync(txt, isRazor: true);
        }
        #endregion

        [Fact]
        public async Task GetSemanticTokens_Razor_CommentAsync()
        {
            var txt = $"@* A comment *@";

            await AssertSemanticTokensAsync(txt, isRazor: true);
        }

        [Fact]
        public async Task GetSemanticTokens_Razor_MultiLineCommentMidlineAsync()
        {
            var txt = $@"<a />@* kdl
   skd
slf*@";

            await AssertSemanticTokensAsync(txt, isRazor: false);
        }

        [Fact]
        public async Task GetSemanticTokens_Razor_MultiLineCommentAsync()
        {
            var txt = $"@* {Environment.NewLine}" +
                $"stuff{Environment.NewLine}" +
                $" things *@";

            await AssertSemanticTokensAsync(txt, isRazor: false);
        }

        private async Task AssertSemanticTokensAsync(
            string txt,
            bool isRazor,
            OmniSharpRange? range = null,
            RazorSemanticTokensInfoService? service = null,
            ProvideSemanticTokensResponse? csharpTokens = null,
            int? documentVersion = 0)
        {
            if (range is null)
            {
                var lines = txt.Split(Environment.NewLine);
                range = new OmniSharpRange { Start = new Position { Line = 0, Character = 0 }, End = new Position { Line = lines.Length - 1, Character = lines[^1].Length } };
            };

            await AssertSemanticTokensAsync(new string[] { txt }, new bool[] { isRazor }, range, service, csharpTokens, documentVersion);
        }

        private async Task AssertSemanticTokensAsync(
            string[] txtArray,
            bool[] isRazorArray,
            OmniSharpRange range,
            RazorSemanticTokensInfoService? service = null,
            ProvideSemanticTokensResponse? csharpTokens = null,
            int? documentVersion = 0)
        {
            // Arrange
            if (documentVersion == 0 && csharpTokens != null)
            {
                documentVersion = (int?)csharpTokens.HostDocumentSyncVersion;
            }

            if (csharpTokens is null)
            {
                csharpTokens = new ProvideSemanticTokensResponse(tokens: null, isFinalized: true, documentVersion);
            }

            var (documentSnapshots, textDocumentIdentifiers) = CreateDocumentSnapshot(txtArray, isRazorArray, DefaultTagHelpers);

            if (service is null)
            {
                service = GetDefaultRazorSemanticTokenInfoService(documentSnapshots, csharpTokens, documentVersion);
            }

            var textDocumentIdentifier = textDocumentIdentifiers.Dequeue();

            // Act
            var tokens = await service.GetSemanticTokensAsync(textDocumentIdentifier, range, CancellationToken.None);

            // Assert
            AssertSemanticTokensMatchesBaseline(tokens?.Data);
        }

        private RazorSemanticTokensInfoService GetDefaultRazorSemanticTokenInfoService(
            Queue<DocumentSnapshot> documentSnapshots,
            ProvideSemanticTokensResponse? csharpTokens = null,
            int? documentVersion = 0)
        {
            var responseRouterReturns = new Mock<IResponseRouterReturns>(MockBehavior.Strict);
            responseRouterReturns
                .Setup(l => l.Returning<ProvideSemanticTokensResponse?>(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(csharpTokens));

            var languageServer = new Mock<ClientNotifierServiceBase>(MockBehavior.Strict);
            languageServer
                .Setup(l => l.SendRequestAsync(LanguageServerConstants.RazorProvideSemanticTokensRangeEndpoint, It.IsAny<SemanticTokensParams>()))
                .Returns(Task.FromResult(responseRouterReturns.Object));

            var documentMappingService = new DefaultRazorDocumentMappingService(TestLoggerFactory.Instance);
            var loggingFactory = TestLoggerFactory.Instance;
            var projectSnapshotManagerDispatcher = LegacyDispatcher;
            var documentResolver = new TestDocumentResolver(documentSnapshots);

            var documentVersionCache = new Mock<DocumentVersionCache>(MockBehavior.Strict);
            documentVersionCache.Setup(c => c.TryGetDocumentVersion(It.IsAny<DocumentSnapshot>(), out documentVersion))
                .Returns(true);

            return new DefaultRazorSemanticTokensInfoService(
                languageServer.Object,
                documentMappingService,
                projectSnapshotManagerDispatcher,
                documentResolver,
                documentVersionCache.Object,
                loggingFactory);
        }

        private class TestDocumentResolver : DocumentResolver
        {
            private readonly Queue<DocumentSnapshot> _documentSnapshots;

            public TestDocumentResolver(Queue<DocumentSnapshot> documentSnapshots)
            {
                _documentSnapshots = documentSnapshots;
            }

            public override bool TryResolveDocument(string documentFilePath, out DocumentSnapshot document)
            {
                document = _documentSnapshots.Count == 1 ? _documentSnapshots.Peek() : _documentSnapshots.Dequeue();

                return true;
            }
        }
    }
}
