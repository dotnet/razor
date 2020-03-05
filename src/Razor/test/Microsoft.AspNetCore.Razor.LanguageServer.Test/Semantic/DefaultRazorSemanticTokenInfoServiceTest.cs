// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Semantic
{
    public class DefaultRazorSemanticTokenInfoServiceTest : DefaultTagHelperServiceTestBase
    {
        public static IEnumerable<object[]> TestCases =>
            new List<object[]>
            {
                new object[] { $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1></test1>",
                new List<uint> {
                    1, 1, 5, 0, 0, //line, character pos, length, tokenType, modifier
                    0, 8, 5, 0, 0
                }},
                new object[] { $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 bool-val='true'></test1>" ,
                new List<uint> {
                    1, 1, 5, 0, 0, //line, character pos, length, tokenType, modifier
                    0, 6, 8, 1, 0,
                    0, 18, 5, 0, 0
                }},
                new object[] { $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 bool-val></test1>",
                new List<uint> {
                    1, 1, 5, 0, 0, //line, character pos, length, tokenType, modifier
                    0, 6, 8, 1, 0,
                    0, 11, 5, 0, 0
                }},
                new object[] { $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 bool-val='true' class='display:none'></test1>",
                new List<uint> {
                    1, 1, 5, 0, 0, //line, character pos, length, tokenType, modifier
                    0, 6, 8, 1, 0,
                    0, 39, 5, 0, 0
                }},
                new object[] { $"@addTagHelper *, TestAssembly{Environment.NewLine}<p bool-val='true'></p>",
                new List<uint> {}}
            };

        public static IEnumerable<object[]> DirectiveCases =>
            new List<object[]>
            {
                new object[] { $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 @onclick='Function'></test1>",
                new List<uint> {
                    1, 1, 5, 1, 0, //line, character pos, length, tokenType, modifier
                    0, 8, 5, 2, 0
                }},
                new object[] { $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 @onclick:preventDefault='Function'></test1>",
                new List<uint> {
                    1, 1, 5, 1, 0, //line, character pos, length, tokenType, modifier
                    0, 8, 5, 2, 0
                }},
            };

        [Theory]
        [MemberData(nameof(TestCases))]
        public void GetSemanticTokens(string txt, IEnumerable<uint> expectedData)
        {
            // Arrange
            var service = GetDefaultRazorSemanticTokenInfoService();
            var codeDocument = CreateCodeDocument(txt, DefaultTagHelpers);
            var location = new SourceLocation(txt.IndexOf("test1"), -1, -1);

            // Act
            var tokens = service.GetSemanticTokens(codeDocument, location);

            // Assert
            Assert.Equal(expectedData, tokens.Data);
        }

        [Theory(Skip = "Haven't implemented directives yet")]
        [MemberData(nameof(DirectiveCases))]
        public void GetSemanticTokens_Directives(string txt, IEnumerable<uint> expectedData)
        {
            // Arrange
            var service = GetDefaultRazorSemanticTokenInfoService();
            var codeDocument = CreateCodeDocument(txt, DefaultTagHelpers);
            var location = new SourceLocation(txt.IndexOf("test1"), -1, -1);

            // Act
            var tokens = service.GetSemanticTokens(codeDocument, location);

            // Assert
            Assert.Equal(expectedData, tokens.Data);
        }

        private RazorSemanticTokenInfoService GetDefaultRazorSemanticTokenInfoService()
        {
            return new DefaultRazorSemanticTokenInfoService();
        }
    }
}
