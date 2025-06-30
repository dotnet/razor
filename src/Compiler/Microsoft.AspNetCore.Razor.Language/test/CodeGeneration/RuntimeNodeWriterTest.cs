// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

public class RuntimeNodeWriterTest : RazorProjectEngineTestBase
{
    protected override RazorLanguageVersion Version => RazorLanguageVersion.Latest;

    protected override void ConfigureCodeDocumentProcessor(RazorCodeDocumentProcessor processor)
    {
        processor.ExecutePhasesThrough<IRazorIntermediateNodeLoweringPhase>();
    }

    [Fact]
    public void WriteUsingDirective_NoSource_WritesContent()
    {
        // Arrange
        using var context = TestCodeRenderingContext.CreateRuntime();
        var writer = new RuntimeNodeWriter(context);

        var node = new UsingDirectiveIntermediateNode()
        {
            Content = "System",
        };

        // Act
        writer.WriteUsingDirective(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"using System;
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteUsingDirective_WithSource_WritesContentWithLinePragma()
    {
        // Arrange
        using var context = TestCodeRenderingContext.CreateRuntime();
        var writer = new RuntimeNodeWriter(context);

        var node = new UsingDirectiveIntermediateNode()
        {
            Content = "System",
            Source = new SourceSpan("test.cshtml", 0, 0, 0, 3)
        };

        // Act
        writer.WriteUsingDirective(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"
#nullable restore
#line (1,1)-(1,1) ""test.cshtml""
using System

#nullable disable
;
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteUsingDirective_WithSourceAndLineDirectives_WritesContentWithLinePragmaAndMapping()
    {
        // Arrange
        using var context = TestCodeRenderingContext.CreateRuntime();
        var writer = new RuntimeNodeWriter(context);

        var node = new UsingDirectiveIntermediateNode()
        {
            Content = "System",
            Source = new SourceSpan("test.cshtml", 0, 0, 0, 3),
            AppendLineDefaultAndHidden = true
        };

        // Act
        writer.WriteUsingDirective(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"
#nullable restore
#line (1,1)-(1,1) ""test.cshtml""
using System

#nullable disable
;
#line default
#line hidden
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCSharpExpression_SkipsLinePragma_WithoutSource()
    {
        // Arrange
        using var context = TestCodeRenderingContext.CreateRuntime();

        var writer = new RuntimeNodeWriter(context)
        {
            WriteCSharpExpressionMethod = "Test"
        };

        var node = new CSharpExpressionIntermediateNode();
        var builder = IntermediateNodeBuilder.Create(node);
        builder.Add(NodeFactory.CSharpToken("i++"));

        // Act
        writer.WriteCSharpExpression(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"Test(
i++);
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCSharpExpression_WritesLinePragma_WithSource()
    {
        // Arrange
        using var context = TestCodeRenderingContext.CreateRuntime();

        var writer = new RuntimeNodeWriter(context)
        {
            WriteCSharpExpressionMethod = "Test"
        };

        var node = new CSharpExpressionIntermediateNode();
        var builder = IntermediateNodeBuilder.Create(node);
        builder.Add(NodeFactory.CSharpToken("i++", new SourceSpan("test.cshtml", 0, 0, 0, 3, 0, 3)));

        // Act
        writer.WriteCSharpExpression(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"Test(
#nullable restore
#line (1,1)-(1,4) ""test.cshtml""
i++

#line default
#line hidden
#nullable disable
);
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCSharpExpression_WithExtensionNode_WritesPadding()
    {
        // Arrange
        using var context = TestCodeRenderingContext.CreateRuntime();

        var writer = new RuntimeNodeWriter(context)
        {
            WriteCSharpExpressionMethod = "Test"
        };

        var node = new CSharpExpressionIntermediateNode();
        var builder = IntermediateNodeBuilder.Create(node);
        builder.Add(NodeFactory.CSharpToken("i"));

        builder.Add(new MyExtensionIntermediateNode());

        builder.Add(NodeFactory.CSharpToken("++"));

        // Act
        writer.WriteCSharpExpression(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"Test(
iRender Children
++);
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCSharpExpression_WithSource_WritesPadding()
    {
        // Arrange
        using var context = TestCodeRenderingContext.CreateRuntime();

        var writer = new RuntimeNodeWriter(context)
        {
            WriteCSharpExpressionMethod = "Test"
        };

        var node = new CSharpExpressionIntermediateNode();
        var builder = IntermediateNodeBuilder.Create(node);
        builder.Add(NodeFactory.CSharpToken("i", new SourceSpan("test.cshtml", 0, 0, 0, 1, 0, 1)));

        builder.Add(new MyExtensionIntermediateNode());

        builder.Add(NodeFactory.CSharpToken("++", new SourceSpan("test.cshtml", 2, 0, 2, 2, 0, 4)));

        // Act
        writer.WriteCSharpExpression(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"Test(
#nullable restore
#line (1,1)-(1,2) ""test.cshtml""
i

#line default
#line hidden
#nullable disable
Render Children
#nullable restore
#line (1,3)-(1,5) ""test.cshtml""
++

#line default
#line hidden
#nullable disable
);
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCSharpCode_WhitespaceContent_DoesNothing()
    {
        // Arrange
        using var context = TestCodeRenderingContext.CreateRuntime();
        var writer = new RuntimeNodeWriter(context);

        var node = new CSharpCodeIntermediateNode();
        IntermediateNodeBuilder.Create(node)
            .Add(NodeFactory.CSharpToken("  \t"));

        // Act
        writer.WriteCSharpCode(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Empty(csharp);
    }

    [Fact]
    public void WriteCSharpCode_SkipsLinePragma_WithoutSource()
    {
        // Arrange
        using var context = TestCodeRenderingContext.CreateRuntime();
        var writer = new RuntimeNodeWriter(context);

        var node = new CSharpCodeIntermediateNode();
        IntermediateNodeBuilder.Create(node)
            .Add(NodeFactory.CSharpToken("if (true) { }"));

        // Act
        writer.WriteCSharpCode(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"if (true) { }
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCSharpCode_WritesLinePragma_WithSource()
    {
        // Arrange
        using var context = TestCodeRenderingContext.CreateRuntime();
        var writer = new RuntimeNodeWriter(context);

        var node = new CSharpCodeIntermediateNode();
        IntermediateNodeBuilder.Create(node)
            .Add(NodeFactory.CSharpToken("if (true) { }", span: new SourceSpan("test.cshtml", 0, 0, 0, 13)));

        // Act
        writer.WriteCSharpCode(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"
#nullable restore
#line (1,1)-(1,1) ""test.cshtml""
if (true) { }

#line default
#line hidden
#nullable disable

",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCSharpCode_WritesPadding_WithSource()
    {
        // Arrange
        using var context = TestCodeRenderingContext.CreateRuntime();
        var writer = new RuntimeNodeWriter(context);

        var node = new CSharpCodeIntermediateNode();
        IntermediateNodeBuilder.Create(node)
            .Add(NodeFactory.CSharpToken("    if (true) { }", span: new SourceSpan("test.cshtml", 0, 0, 0, 17)));

        // Act
        writer.WriteCSharpCode(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"
#nullable restore
#line (1,1)-(1,1) ""test.cshtml""
    if (true) { }

#line default
#line hidden
#nullable disable

",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteHtmlLiteral_WithinMaxSize_WritesSingleLiteral()
    {
        // Arrange
        using var context = TestCodeRenderingContext.CreateRuntime();
        var writer = new RuntimeNodeWriter(context);

        // Act
        writer.WriteHtmlLiteral(context, maxStringLiteralLength: 6, "Hello".AsMemory());

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"WriteLiteral(""Hello"");
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteHtmlLiteral_GreaterThanMaxSize_WritesMultipleLiterals()
    {
        // Arrange
        using var context = TestCodeRenderingContext.CreateRuntime();
        var writer = new RuntimeNodeWriter(context);

        // Act
        writer.WriteHtmlLiteral(context, maxStringLiteralLength: 6, "Hello World".AsMemory());

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"WriteLiteral(""Hello "");
WriteLiteral(""World"");
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteHtmlLiteral_GreaterThanMaxSize_SingleEmojisSplit()
    {
        // Arrange
        using var context = TestCodeRenderingContext.CreateRuntime();
        var writer = new RuntimeNodeWriter(context);

        // Act
        writer.WriteHtmlLiteral(context, maxStringLiteralLength: 2, " 👦".AsMemory());

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"WriteLiteral("" "");
WriteLiteral(""👦"");
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteHtmlLiteral_GreaterThanMaxSize_SequencedZeroWithJoinedEmojisSplit()
    {
        // Arrange
        using var context = TestCodeRenderingContext.CreateRuntime();
        var writer = new RuntimeNodeWriter(context);

        // Act
        writer.WriteHtmlLiteral(context, maxStringLiteralLength: 6, "👩‍👩‍👧‍👧👩‍👩‍👧‍👧".AsMemory());

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"WriteLiteral(""👩‍👩‍"");
WriteLiteral(""👧‍👧"");
WriteLiteral(""👩‍👩‍"");
WriteLiteral(""👧‍👧"");
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteHtmlContent_RendersContentCorrectly()
    {
        // Arrange
        using var context = TestCodeRenderingContext.CreateRuntime();
        var writer = new RuntimeNodeWriter(context);

        var node = new HtmlContentIntermediateNode();
        node.Children.Add(NodeFactory.HtmlToken("SomeContent"));

        // Act
        writer.WriteHtmlContent(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"WriteLiteral(""SomeContent"");
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteHtmlContent_LargeStringLiteral_UsesMultipleWrites()
    {
        // Arrange
        using var context = TestCodeRenderingContext.CreateRuntime();
        var writer = new RuntimeNodeWriter(context);

        var node = new HtmlContentIntermediateNode();
        node.Children.Add(NodeFactory.HtmlToken(new string('*', 2000)));

        // Act
        writer.WriteHtmlContent(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(string.Format(
            CultureInfo.InvariantCulture,
@"WriteLiteral(@""{0}"");
WriteLiteral(@""{1}"");
", new string('*', 1024), new string('*', 976)),
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteHtmlAttribute_RendersCorrectly()
    {
        // Arrange
        var content = "<input checked=\"hello-world @false\" />";
        var source = TestRazorSourceDocument.Create(content);
        var codeDocument = ProjectEngine.CreateCodeDocument(source);
        var processor = CreateCodeDocumentProcessor(codeDocument);
        var documentNode = processor.GetDocumentNode();
        var node = documentNode.Children.OfType<HtmlAttributeIntermediateNode>().Single();

        using var context = TestCodeRenderingContext.CreateRuntime();
        var writer = new RuntimeNodeWriter(context);

        // Act
        writer.WriteHtmlAttribute(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"BeginWriteAttribute(""checked"", "" checked=\"""", 6, ""\"""", 34, 2);
Render Children
Render Children
EndWriteAttribute();
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteHtmlAttributeValue_RendersCorrectly()
    {
        // Arrange
        var content = "<input checked=\"hello-world @false\" />";
        var source = TestRazorSourceDocument.Create(content);
        var codeDocument = ProjectEngine.CreateCodeDocument(source);
        var processor = CreateCodeDocumentProcessor(codeDocument);
        var documentNode = processor.GetDocumentNode();
        var node = (HtmlAttributeValueIntermediateNode)documentNode.Children.OfType<HtmlAttributeIntermediateNode>().Single().Children[0];

        using var context = TestCodeRenderingContext.CreateRuntime();
        var writer = new RuntimeNodeWriter(context);

        // Act
        writer.WriteHtmlAttributeValue(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"WriteAttributeValue("""", 16, ""hello-world"", 16, 11, true);
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCSharpExpressionAttributeValue_RendersCorrectly()
    {
        // Arrange
        var content = "<input checked=\"hello-world @false\" />";
        var source = TestRazorSourceDocument.Create(content);
        var codeDocument = ProjectEngine.CreateCodeDocument(source);
        var processor = CreateCodeDocumentProcessor(codeDocument);
        var documentNode = processor.GetDocumentNode();
        var node = (CSharpExpressionAttributeValueIntermediateNode)documentNode.Children.OfType<HtmlAttributeIntermediateNode>().Single().Children[1];

        using var context = TestCodeRenderingContext.CreateRuntime();
        var writer = new RuntimeNodeWriter(context);

        // Act
        writer.WriteCSharpExpressionAttributeValue(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"WriteAttributeValue("" "", 27, 
#nullable restore
#line (1,30)-(1,35) ""test.cshtml""
false

#line default
#line hidden
#nullable disable
, 28, 6, false);
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteCSharpCodeAttributeValue_BuffersResult()
    {
        // Arrange
        var content = "<input checked=\"hello-world @if(@true){ }\" />";
        var source = TestRazorSourceDocument.Create(content);
        var codeDocument = ProjectEngine.CreateCodeDocument(source);
        var processor = CreateCodeDocumentProcessor(codeDocument);
        var documentNode = processor.GetDocumentNode();
        var node = (CSharpCodeAttributeValueIntermediateNode)documentNode.Children.OfType<HtmlAttributeIntermediateNode>().Single().Children[1];

        using var context = TestCodeRenderingContext.CreateRuntime(source: source);
        var writer = new RuntimeNodeWriter(context);

        // Act
        writer.WriteCSharpCodeAttributeValue(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"WriteAttributeValue("" "", 27, new Microsoft.AspNetCore.Mvc.Razor.HelperResult(async(__razor_attribute_value_writer) => {
    PushWriter(__razor_attribute_value_writer);
#nullable restore
#line (1,30)-(1,42) ""test.cshtml""
if(@true){ }

#line default
#line hidden
#nullable disable
    PopWriter();
}
), 28, 13, false);
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void BeginWriterScope_UsesSpecifiedWriter_RendersCorrectly()
    {
        // Arrange
        using var context = TestCodeRenderingContext.CreateRuntime();

        var writer = new RuntimeNodeWriter(context)
        {
            PushWriterMethod = "TestPushWriter"
        };

        // Act
        writer.BeginWriterScope(context, "MyWriter");

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"TestPushWriter(MyWriter);
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void EndWriterScope_RendersCorrectly()
    {
        // Arrange
        using var context = TestCodeRenderingContext.CreateRuntime();

        var writer = new RuntimeNodeWriter(context)
        {
            PopWriterMethod = "TestPopWriter"
        };

        // Act
        writer.EndWriterScope(context);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"TestPopWriter();
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    private class MyExtensionIntermediateNode : ExtensionIntermediateNode
    {
        public override IntermediateNodeCollection Children => IntermediateNodeCollection.ReadOnly;

        public override void Accept(IntermediateNodeVisitor visitor)
        {
            visitor.VisitDefault(this);
        }

        public override void WriteNode(CodeTarget target, CodeRenderingContext context)
        {
            throw new NotImplementedException();
        }
    }
}
