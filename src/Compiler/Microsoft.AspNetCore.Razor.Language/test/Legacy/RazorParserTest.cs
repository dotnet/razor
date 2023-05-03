﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Xunit;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

public class RazorParserTest
{
    [Fact]
    public void CanParseStuff()
    {
        var parser = new RazorParser();
        var sourceDocument = TestRazorSourceDocument.CreateResource("TestFiles/Source/BasicMarkup.cshtml", GetType());
        var output = parser.Parse(sourceDocument);

        Assert.NotNull(output);
    }

    [Fact]
    public void ParseMethodCallsParseDocumentOnMarkupParserAndReturnsResults()
    {
        // Arrange
        var parser = new RazorParser();
        var expected =
@"RazorDocument - [0..12)::12 - [foo @bar baz]
    MarkupBlock - [0..12)::12
        MarkupTextLiteral - [0..4)::4 - [foo ] - Gen<Markup> - SpanEditHandler;Accepts:Any
            Text;[foo];
            Whitespace;[ ];
        CSharpCodeBlock - [4..8)::4
            CSharpImplicitExpression - [4..8)::4
                CSharpTransition - [4..5)::1 - Gen<None> - SpanEditHandler;Accepts:None
                    Transition;[@];
                CSharpImplicitExpressionBody - [5..8)::3
                    CSharpCodeBlock - [5..8)::3
                        CSharpExpressionLiteral - [5..8)::3 - [bar] - Gen<Expr> - ImplicitExpressionEditHandler;Accepts:NonWhitespace;ImplicitExpression[RTD];K14
                            Identifier;[bar];
        MarkupTextLiteral - [8..12)::4 - [ baz] - Gen<Markup> - SpanEditHandler;Accepts:Any
            Whitespace;[ ];
            Text;[baz];
    EndOfFile;[];
";

        // Act
        var syntaxTree = parser.Parse(TestRazorSourceDocument.Create("foo @bar baz"));

        // Assert
        var actual = SyntaxNodeSerializer.Serialize(syntaxTree.Root);
        Assert.Equal(expected, actual);
    }
}
