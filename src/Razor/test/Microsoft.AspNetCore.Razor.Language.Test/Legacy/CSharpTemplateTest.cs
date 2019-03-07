// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language.Legacy
{
    public class CSharpTemplateTest : ParserTestBase
    {
        [Fact]
        public void HandlesSingleLineTemplate()
        {
            ParseDocumentTest("@{ var foo = @: bar" + Environment.NewLine + "; }");
        }

        [Fact]
        public void HandlesSingleLineImmediatelyFollowingStatementChar()
        {
            ParseDocumentTest("@{i@: bar" + Environment.NewLine + "}");
        }

        [Fact]
        public void HandlesSimpleTemplateInExplicitExpressionParens()
        {
            ParseDocumentTest("@(Html.Repeat(10, @<p>Foo #@item</p>))");
        }

        [Fact]
        public void HandlesSimpleTemplateInImplicitExpressionParens()
        {
            ParseDocumentTest("@Html.Repeat(10, @<p>Foo #@item</p>)");
        }

        [Fact]
        public void HandlesTwoTemplatesInImplicitExpressionParens()
        {
            ParseDocumentTest("@Html.Repeat(10, @<p>Foo #@item</p>, @<p>Foo #@item</p>)");
        }

        [Fact]
        public void ProducesErrorButCorrectlyParsesNestedTemplateInImplicitExprParens()
        {
            // ParseBlockProducesErrorButCorrectlyParsesNestedTemplateInImplicitExpressionParens
            ParseDocumentTest("@Html.Repeat(10, @<p>Foo #@Html.Repeat(10, @<p>@item</p>)</p>)");
        }

        [Fact]
        public void HandlesSimpleTemplateInStatementWithinCodeBlock()
        {
            ParseDocumentTest("@foreach(foo in Bar) { Html.ExecuteTemplate(foo, @<p>Foo #@item</p>); }");
        }

        [Fact]
        public void HandlesTwoTemplatesInStatementWithinCodeBlock()
        {
            ParseDocumentTest("@foreach(foo in Bar) { Html.ExecuteTemplate(foo, @<p>Foo #@item</p>, @<p>Foo #@item</p>); }");
        }

        [Fact]
        public void ProducesErrorButCorrectlyParsesNestedTemplateInStmtWithinCodeBlock()
        {
            // ParseBlockProducesErrorButCorrectlyParsesNestedTemplateInStatementWithinCodeBlock
            ParseDocumentTest("@foreach(foo in Bar) { Html.ExecuteTemplate(foo, @<p>Foo #@Html.Repeat(10, @<p>@item</p>)</p>); }");
        }

        [Fact]
        public void HandlesSimpleTemplateInStatementWithinStatementBlock()
        {
            ParseDocumentTest("@{ var foo = bar; Html.ExecuteTemplate(foo, @<p>Foo #@item</p>); }");
        }

        [Fact]
        public void HandlessTwoTemplatesInStatementWithinStatementBlock()
        {
            ParseDocumentTest("@{ var foo = bar; Html.ExecuteTemplate(foo, @<p>Foo #@item</p>, @<p>Foo #@item</p>); }");
        }

        [Fact]
        public void ProducesErrorButCorrectlyParsesNestedTemplateInStmtWithinStmtBlock()
        {
            // ParseBlockProducesErrorButCorrectlyParsesNestedTemplateInStatementWithinStatementBlock
            ParseDocumentTest("@{ var foo = bar; Html.ExecuteTemplate(foo, @<p>Foo #@Html.Repeat(10, @<p>@item</p>)</p>); }");
        }

        [Fact]
        public void _WithDoubleTransition_DoesNotThrow()
        {
            ParseDocumentTest("@{ var foo = bar; Html.ExecuteTemplate(foo, @<p foo='@@'>Foo #@item</p>); }");
        }

        [Fact]
        public void TemplateInFunctionsBlock_DoesNotParseWhenNotSupported()
        {
            ParseDocumentTest(
                RazorLanguageVersion.Version_2_1,
                @"
@functions {
    void Announcment(string message)
    {
        @<h3>@message</h3>
    }
}
", new[] { FunctionsDirective.Directive, }, designTime: false);
        }

        [Fact]
        public void TemplateInFunctionsBlock_ParsesTemplateInsideMethod()
        {
            ParseDocumentTest(
                RazorLanguageVersion.Version_3_0, 
                @"
@functions {
    void Announcment(string message)
    {
        @<h3>@message</h3>
    }
}
", new[] { FunctionsDirective.Directive, }, designTime: false);
        }

        // This will parse correctly in Razor, but will generate invalid C#.
        [Fact]
        public void TemplateInFunctionsBlock_ParsesTemplateWithExpressionsMethod()
        {
            ParseDocumentTest(
                RazorLanguageVersion.Version_3_0, 
                @"
@functions {
    void Announcment(string message) => @<h3>@message</h3>
}
", new[] { FunctionsDirective.Directive, }, designTime: false);
        }

        [Fact]
        public void TemplateInFunctionsBlock_DoesNotParseTemplateInString()
        {
            ParseDocumentTest(
                RazorLanguageVersion.Version_3_0,
                @"
@functions {
    void Announcment(string message) => ""@<h3>@message</h3>"";
}
", new[] { FunctionsDirective.Directive, }, designTime: false);
        }

        [Fact]
        public void TemplateInFunctionsBlock_DoesNotParseTemplateInVerbatimString()
        {
            ParseDocumentTest(
                RazorLanguageVersion.Version_3_0,
                @"
@functions {
    void Announcment(string message) => @""@<h3>@message</h3>"";
}
", new[] { FunctionsDirective.Directive, }, designTime: false);
        }

        [Fact]
        public void TemplateInFunctionsBlock_TemplateCanContainCurlyBraces()
        {
            ParseDocumentTest(
                RazorLanguageVersion.Version_3_0, 
                @"
@functions {
    void Announcment(string message)
    {
        @<div>
            @if (message.Length > 0)
            {
                <p>@message.Length</p>
            }
        </div>
    }
}
", new[] { FunctionsDirective.Directive, }, designTime: false);
        }

        [Fact]
        public void TemplateInFunctionsBlock_TemplateCanContainTemplate()
        {
            ParseDocumentTest(
                RazorLanguageVersion.Version_3_0, 
                @"
@functions {
    void Announcment(string message)
    {
        @<div>
            @if (message.Length > 0)
            {
                Repeat(@<p>@message.Length</p>);
            }
        </div>
    }
}
", new[] { FunctionsDirective.Directive, }, designTime: false);
        }
    }
}
