// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
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

            await AssertSemanticTokensAsync(txt, isRazor: false, csharpTokens: cSharpResponse, documentMappings: null, documentVersion: 1);
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
                14, 0, 3, RazorSemanticTokensLegend.CSharpKeyword, 0,
                1, 0, 1, RazorSemanticTokensLegend.CSharpVariable, 0,
                1, 0, 1, RazorSemanticTokensLegend.CSharpPunctuation, 0,
                1, 0, 1, RazorSemanticTokensLegend.CSharpPunctuation, 0,
                1, 0, 1, RazorSemanticTokensLegend.CSharpString, 0,
                1, 0, 1, RazorSemanticTokensLegend.CSharpPunctuation, 0,
                1, 0, 1, RazorSemanticTokensLegend.CSharpPunctuation, 0,
                1, 0, 1, RazorSemanticTokensLegend.CSharpVariable, 0,
            };

            var cSharpResponse = new ProvideSemanticTokensResponse(
                csharpTokens, isFinalized: true, hostDocumentSyncVersion: 0);

            var mappings = new (OmniSharpRange, OmniSharpRange?)[] {
                (new OmniSharpRange(new Position(14, 0), new Position(14, 3)), new OmniSharpRange(new Position(1, 4), new Position(1, 7))),
                (new OmniSharpRange(new Position(15, 0), new Position(15, 1)), new OmniSharpRange(new Position(1, 8), new Position(1,9))),
                (new OmniSharpRange(new Position(16, 0), new Position(16, 1)), new OmniSharpRange(new Position(1, 10), new Position(1, 11))),
                (new OmniSharpRange(new Position(17, 0), new Position(17, 1)), new OmniSharpRange(new Position(1, 12), new Position(1, 13))),
                (new OmniSharpRange(new Position(18, 0), new Position(18, 1)), new OmniSharpRange(new Position(1, 13), new Position(1, 14))),
                (new OmniSharpRange(new Position(19, 0), new Position(19, 1)), new OmniSharpRange(new Position(1, 14), new Position(1, 15))),
                (new OmniSharpRange(new Position(20, 0), new Position(20, 1)), new OmniSharpRange(new Position(1, 15), new Position(1, 16))),
                (new OmniSharpRange(new Position(21, 0), new Position(21, 1)), new OmniSharpRange(new Position(2, 13), new Position(2, 14))),
            };

            await AssertSemanticTokensAsync(txt, isRazor: false, csharpTokens: cSharpResponse, documentMappings: mappings);
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
                14, 0, 3, RazorSemanticTokensLegend.CSharpKeyword, 0,
                1, 0, 1, RazorSemanticTokensLegend.CSharpVariable, 0,
                1, 0, 1, RazorSemanticTokensLegend.CSharpOperator, 0,
                1, 0, 1, RazorSemanticTokensLegend.CSharpPunctuation, 0,
                1, 0, 6, RazorSemanticTokensLegend.CSharpString, 0,
                1, 0, 1, RazorSemanticTokensLegend.CSharpPunctuation, 0,
                1, 0, 1, RazorSemanticTokensLegend.CSharpPunctuation, 0,
            };

            var cSharpResponse = new ProvideSemanticTokensResponse(
                csharpTokens, isFinalized: true, hostDocumentSyncVersion: 0);

            var mappings = new (OmniSharpRange, OmniSharpRange?)[] {
                (new OmniSharpRange(new Position(14, 0), new Position(14, 3)), new OmniSharpRange(new Position(1, 6), new Position(1, 9))),
                (new OmniSharpRange(new Position(15, 0), new Position(15, 1)), new OmniSharpRange(new Position(1, 10), new Position(1, 11))),
                (new OmniSharpRange(new Position(16, 0), new Position(16, 1)), new OmniSharpRange(new Position(1, 12), new Position(1, 13))),
                (new OmniSharpRange(new Position(17, 0), new Position(17, 1)), new OmniSharpRange(new Position(1, 13), new Position(1, 14))),
                (new OmniSharpRange(new Position(18, 0), new Position(18, 6)), new OmniSharpRange(new Position(1, 14), new Position(1, 20))),
                (new OmniSharpRange(new Position(19, 0), new Position(19, 1)), new OmniSharpRange(new Position(1, 20), new Position(1, 21))),
                (new OmniSharpRange(new Position(20, 0), new Position(20, 1)), new OmniSharpRange(new Position(1, 21), new Position(1, 22))),
            };

            await AssertSemanticTokensAsync(txt, isRazor: false, csharpTokens: cSharpResponse, documentMappings: mappings);
        }

        [Fact]
        public async Task GetSemanticTokens_CSharp_VSCodeWorks()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}@{{ var d = }}";

            var cSharpResponse = new ProvideSemanticTokensResponse(
                tokens: Array.Empty<int>(), isFinalized: true, hostDocumentSyncVersion: null);

            var mappings = Array.Empty<(OmniSharpRange, OmniSharpRange?)>();

            await AssertSemanticTokensAsync(txt, isRazor: false, csharpTokens: cSharpResponse, documentMappings: mappings, documentVersion: 1);
        }

        [Fact]
        public async Task GetSemanticTokens_CSharp_Explicit()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}@(DateTime.Now)";

            var csharpTokens = new int[]
            {
                14, 0, 8, RazorSemanticTokensLegend.CSharpVariable, 0,
                1, 0, 1, RazorSemanticTokensLegend.CSharpPunctuation, 0,
                1, 0, 3, RazorSemanticTokensLegend.CSharpVariable, 0,
            };

            var cSharpResponse = new ProvideSemanticTokensResponse(csharpTokens, isFinalized: true, hostDocumentSyncVersion: 0);

            var mappings = new (OmniSharpRange, OmniSharpRange?)[] {
                (new OmniSharpRange(new Position(14, 0),new Position(14, 8)), new OmniSharpRange(new Position(1, 2), new Position(1, 10))),
                (new OmniSharpRange(new Position(15, 0),new Position(15, 1)), new OmniSharpRange(new Position(1, 10), new Position(1, 11))),
                (new OmniSharpRange(new Position(16, 0),new Position(16, 3)), new OmniSharpRange(new Position(1, 11), new Position(1, 14))),
            };

            await AssertSemanticTokensAsync(txt, isRazor: false, csharpTokens: cSharpResponse, documentMappings: mappings);
        }

        [Fact]
        public async Task GetSemanticTokens_CSharp_Implicit()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}@{{ var d = \"txt\";}}{Environment.NewLine}" +
                $"@d";

            var csharpTokens = new int[]
            {
                14, 0, 3, RazorSemanticTokensLegend.CSharpKeyword, 0,
                1, 0, 1, RazorSemanticTokensLegend.CSharpVariable, 0,
                1, 0, 1, RazorSemanticTokensLegend.CSharpOperator, 0,
                1, 0, 1, RazorSemanticTokensLegend.CSharpOperator, 0,
                1, 0, 3, RazorSemanticTokensLegend.CSharpString, 0,
                1, 0, 1, RazorSemanticTokensLegend.CSharpOperator, 0,
                1, 0, 1, RazorSemanticTokensLegend.CSharpOperator, 0,
                1, 0, 1, RazorSemanticTokensLegend.CSharpVariable, 0,
            };

            var cSharpResponse = new ProvideSemanticTokensResponse(csharpTokens, isFinalized: true, hostDocumentSyncVersion: 0);

            var mappings = new (OmniSharpRange, OmniSharpRange?)[] {
                 (new OmniSharpRange(new Position(14, 0), new Position(14, 3)), new OmniSharpRange(new Position(1, 3), new Position(1, 6))),
                (new OmniSharpRange(new Position(15, 0), new Position(15, 1)), new OmniSharpRange(new Position(1, 7), new Position(1, 8))),
                (new OmniSharpRange(new Position(16, 0), new Position(16, 1)), new OmniSharpRange(new Position(1, 9), new Position(1, 10))),
                (new OmniSharpRange(new Position(17, 0), new Position(17, 1)), new OmniSharpRange(new Position(1, 11), new Position(1, 12))),
                (new OmniSharpRange(new Position(18, 0), new Position(18, 3)), new OmniSharpRange(new Position(1, 12), new Position(1, 15))),
                (new OmniSharpRange(new Position(19, 0), new Position(19, 1)), new OmniSharpRange(new Position(1, 15), new Position(1, 16))),
                (new OmniSharpRange(new Position(20, 0), new Position(20, 1)), new OmniSharpRange(new Position(1, 16), new Position(1, 17))),
                (new OmniSharpRange(new Position(21, 0), new Position(21, 1)), new OmniSharpRange(new Position(2, 1), new Position(2, 2))),
            };

            await AssertSemanticTokensAsync(txt, isRazor: false, csharpTokens: cSharpResponse, documentMappings: mappings);
        }

        [Fact]
        public async Task GetSemanticTokens_CSharp_VersionMismatch()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}@{{ var d = }}";

            var csharpTokens = new int[]
            {
                14, 12, 3, RazorSemanticTokensLegend.CSharpKeyword, 0,
                13, 15, 1, RazorSemanticTokensLegend.CSharpVariable, 0,
                12, 25, 1, RazorSemanticTokensLegend.CSharpOperator, 0,
                11, 10, 25, RazorSemanticTokensLegend.CSharpKeyword, 0, // No mapping
            };

            var cSharpResponse = new ProvideSemanticTokensResponse(csharpTokens, isFinalized: true, hostDocumentSyncVersion: 42);

            var mappings = new (OmniSharpRange, OmniSharpRange?)[] {
               (new OmniSharpRange(new Position(14, 12), new Position(14, 15)), new OmniSharpRange(new Position(1, 3), new Position(1, 6))),
               (new OmniSharpRange(new Position(27, 15), new Position(27, 16)), new OmniSharpRange(new Position(1, 7), new Position(1, 8))),
               (new OmniSharpRange(new Position(39, 25), new Position(39, 26)), new OmniSharpRange(new Position(1, 9), new Position(1, 10))),
               (new OmniSharpRange(new Position(50, 10), new Position(50, 35)), null)
            };

            await AssertSemanticTokensAsync(txt, isRazor: false, csharpTokens: cSharpResponse, documentMappings: mappings, documentVersion: 21);
        }

        [Fact]
        public async Task GetSemanticTokens_CSharp_FunctionAsync()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}@{{ var d = }}";

            var csharpTokens = new int[]
            {
                14, 12, 3, RazorSemanticTokensLegend.CSharpKeyword, 0,
                13, 15, 1, RazorSemanticTokensLegend.CSharpVariable, 0,
                12, 25, 1, RazorSemanticTokensLegend.CSharpOperator, 0,
                11, 10, 25, RazorSemanticTokensLegend.CSharpKeyword, 0, // No mapping
            };

            var cSharpResponse = new ProvideSemanticTokensResponse(csharpTokens, isFinalized: true, hostDocumentSyncVersion: 0);

            var mappings = new (OmniSharpRange, OmniSharpRange?)[] {
               (new OmniSharpRange(new Position(14, 12), new Position(14, 15)), new OmniSharpRange(new Position(1, 3), new Position(1, 6))),
               (new OmniSharpRange(new Position(27, 15), new Position(27, 16)), new OmniSharpRange(new Position(1, 7), new Position(1, 8))),
               (new OmniSharpRange(new Position(39, 25), new Position(39, 26)), new OmniSharpRange(new Position(1, 9), new Position(1, 10))),
               (new OmniSharpRange(new Position(50, 10), new Position(50, 35)), null)
            };

            await AssertSemanticTokensAsync(txt, isRazor: false, csharpTokens: cSharpResponse, documentMappings: mappings);
        }

        [Fact]
        public async Task GetSemanticTokens_CSharp_StaticModifierAsync()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}@{{ var d = }}";

            var csharpTokens = new int[]
            {
                14, 12, 3, RazorSemanticTokensLegend.CSharpKeyword, 0,
                13, 15, 1, RazorSemanticTokensLegend.CSharpVariable, 1,
                12, 25, 1, RazorSemanticTokensLegend.CSharpOperator, 0,
                11, 10, 25, RazorSemanticTokensLegend.CSharpKeyword, 0, // No mapping
            };

            var cSharpResponse = new ProvideSemanticTokensResponse(csharpTokens, isFinalized: true, hostDocumentSyncVersion: 0);

            var mappings = new (OmniSharpRange, OmniSharpRange?)[] {
               (new OmniSharpRange(new Position(14, 12), new Position(14, 15)), new OmniSharpRange(new Position(1, 3), new Position(1, 6))),
               (new OmniSharpRange(new Position(27, 15), new Position(27, 16)), new OmniSharpRange(new Position(1, 7), new Position(1, 8))),
               (new OmniSharpRange(new Position(39, 25), new Position(39, 26)), new OmniSharpRange(new Position(1, 9), new Position(1, 10))),
               (new OmniSharpRange(new Position(50, 10), new Position(50, 35)), null)
            };

            await AssertSemanticTokensAsync(txt, isRazor: false, csharpTokens: cSharpResponse, documentMappings: mappings);
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
        public async Task GetSemanticTokens_HTMLCommentWithCSharp()
        {
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<!-- @DateTime.Now -->";

            var csharpTokens = new int[]
            {
                14, 12, 8, 1, 0, // CSharpType
                0, 8, 1, 21, 0, // operator
                0, 1, 3, 9, 0, // property
            };

            var cSharpResponse = new ProvideSemanticTokensResponse(csharpTokens, isFinalized: true, hostDocumentSyncVersion: 0);

            var mappings = new (OmniSharpRange, OmniSharpRange?)[] {
                (new OmniSharpRange(new Position(14, 12), new Position(14, 20)), new OmniSharpRange(new Position(1, 6), new Position(1, 14))),
                (new OmniSharpRange(new Position(14, 20), new Position(14, 21)), new OmniSharpRange(new Position(1, 14), new Position(1, 15))),
                (new OmniSharpRange(new Position(14, 21),  new Position(14, 24)), new OmniSharpRange(new Position(1, 15), new Position(1, 18))),
            };

            await AssertSemanticTokensAsync(txt, isRazor: true, csharpTokens: cSharpResponse, documentMappings: mappings);
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

        private Task<(string?, RazorSemanticTokensInfoService, Mock<ClientNotifierServiceBase>, Queue<DocumentSnapshot>)> AssertSemanticTokensAsync(
            string txt,
            bool isRazor,
            OmniSharpRange? range = null,
            RazorSemanticTokensInfoService? service = null,
            ProvideSemanticTokensResponse? csharpTokens = null,
            (OmniSharpRange, OmniSharpRange?)[]? documentMappings = null,
            int? documentVersion = 0)
        {
            if (range is null)
            {
                var lines = txt.Split(Environment.NewLine);
                range = new OmniSharpRange { Start = new Position { Line = 0, Character = 0 }, End = new Position { Line = lines.Length - 1, Character = lines[^1].Length } };
            };

            return AssertSemanticTokensAsync(new string[] { txt }, new bool[] { isRazor }, range, service, csharpTokens, documentMappings, documentVersion);
        }

        private async Task<(string?, RazorSemanticTokensInfoService, Mock<ClientNotifierServiceBase>, Queue<DocumentSnapshot>)> AssertSemanticTokensAsync(
            string[] txtArray,
            bool[] isRazorArray,
            OmniSharpRange range,
            RazorSemanticTokensInfoService? service = null,
            ProvideSemanticTokensResponse? csharpTokens = null,
            (OmniSharpRange, OmniSharpRange?)[]? documentMappings = null,
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

            Mock<ClientNotifierServiceBase>? serviceMock = null;
            var (documentSnapshots, textDocumentIdentifiers) = CreateDocumentSnapshot(txtArray, isRazorArray, DefaultTagHelpers);

            if (service is null)
            {
                (service, serviceMock) = await GetDefaultRazorSemanticTokenInfoServiceAsync(
                    documentSnapshots, csharpTokens, documentMappings, documentVersion).ConfigureAwait(false);
            }

            var outService = service;

            var textDocumentIdentifier = textDocumentIdentifiers.Dequeue();

            // Act
            var tokens = await service.GetSemanticTokensAsync(textDocumentIdentifier, range, CancellationToken.None);

            // Assert
            AssertSemanticTokensMatchesBaseline(tokens?.Data);

            return (tokens?.ResultId, outService, serviceMock!, documentSnapshots);
        }

        private async Task<(RazorSemanticTokensInfoService, Mock<ClientNotifierServiceBase>)> GetDefaultRazorSemanticTokenInfoServiceAsync(
            Queue<DocumentSnapshot> documentSnapshots,
            ProvideSemanticTokensResponse? csharpTokens = null,
            (OmniSharpRange, OmniSharpRange?)[]? documentMappings = null,
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
            var documentMappingService = new Mock<RazorDocumentMappingService>(MockBehavior.Strict);
            if (documentMappings is not null)
            {
                foreach (var (cSharpRange, razorRange) in documentMappings)
                {
                    var passingRange = razorRange;
                    documentMappingService
                        .Setup(s => s.TryMapFromProjectedDocumentRange(It.IsAny<RazorCodeDocument>(), cSharpRange, out passingRange))
                        .Returns(razorRange != null);
                }
            }

            foreach (var snapshot in documentSnapshots)
            {
                var codeDocument = await snapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
                if (codeDocument is not null)
                {
                    var sourceText = codeDocument.GetSourceText();
                    var lastLine = sourceText.Lines.Last();
                    var projectedRange = new OmniSharpRange
                    {
                        Start = new Position(0, 0),
                        End = new Position(sourceText.Lines.Count - 1, character: lastLine.Span.Length),
                    };

                    documentMappingService
                        .Setup(s => s.TryMapToProjectedDocumentRange(codeDocument, It.IsAny<OmniSharpRange>(), out projectedRange))
                        .Returns(true);
                }
            }

            var loggingFactory = new Mock<LoggerFactory>(MockBehavior.Strict);
            loggingFactory.Protected().Setup("CheckDisposed").CallBase();

            var projectSnapshotManagerDispatcher = LegacyDispatcher;

            var documentResolver = new TestDocumentResolver(documentSnapshots);

            var documentVersionCache = new Mock<DocumentVersionCache>(MockBehavior.Strict);
            documentVersionCache.Setup(c => c.TryGetDocumentVersion(It.IsAny<DocumentSnapshot>(), out documentVersion))
                .Returns(true);

            return (new DefaultRazorSemanticTokensInfoService(
                languageServer.Object,
                documentMappingService.Object,
                projectSnapshotManagerDispatcher,
                documentResolver,
                documentVersionCache.Object,
                loggingFactory.Object), languageServer);
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
