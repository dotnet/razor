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
                    1, 1, 5, 1, 0, //line, character pos, length, tokenType, modifier
                    0, 8, 5, 2, 0
                }},
                new object[] { $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 bool-val='true'></test1>" ,
                new List<uint> {
                    1, 1, 5, 1, 0, //line, character pos, length, tokenType, modifier
                    0, 6, 8, 3, 0,
                    0, 18, 5, 2, 0
                }},
                new object[] { $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 bool-val></test1>",
                new List<uint> {
                    1, 1, 5, 1, 0, //line, character pos, length, tokenType, modifier
                    0, 6, 8, 5, 0,
                    0, 11, 5, 2, 0
                }},
                new object[] { $"@addTagHelper *, TestAssembly{Environment.NewLine}<p bool-val='true'></p>",
                new List<uint> {}}
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

        [Fact(Skip = "Haven't implemented transitions yet")]
        public void GetSemanticTokens_ColorizeTransition()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "Haven't completed directive attributes yet")]
        public void GetSemanticTokens_ColorizeDirectiveAttribute_WithoutColorizingElement()
        {
            throw new NotImplementedException();
        }

        private RazorSemanticTokenInfoService GetDefaultRazorSemanticTokenInfoService()
        {
            return new DefaultRazorSemanticTokenInfoService();
        }
    }
}
