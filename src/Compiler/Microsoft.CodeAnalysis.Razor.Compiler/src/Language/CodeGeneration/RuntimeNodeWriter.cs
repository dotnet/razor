// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

public class RuntimeNodeWriter(CodeRenderingContext context) : IntermediateNodeWriter(context)
{
    public virtual string WriteCSharpExpressionMethod { get; set; } = "Write";

    public virtual string WriteHtmlContentMethod { get; set; } = "WriteLiteral";

    public virtual string BeginWriteAttributeMethod { get; set; } = "BeginWriteAttribute";

    public virtual string EndWriteAttributeMethod { get; set; } = "EndWriteAttribute";

    public virtual string WriteAttributeValueMethod { get; set; } = "WriteAttributeValue";

    public virtual string PushWriterMethod { get; set; } = "PushWriter";

    public virtual string PopWriterMethod { get; set; } = "PopWriter";

    public string TemplateTypeName { get; set; } = "Microsoft.AspNetCore.Mvc.Razor.HelperResult";

    public override void WriteUsingDirective(UsingDirectiveIntermediateNode node)
    {
        if (node.Source is { FilePath: not null } sourceSpan)
        {
            using (Context.CodeWriter.BuildEnhancedLinePragma(sourceSpan, Context, suppressLineDefaultAndHidden: true))
            {
                Context.CodeWriter.WriteUsing(node.Content, endLine: node.HasExplicitSemicolon);
            }
            if (!node.HasExplicitSemicolon)
            {
                Context.CodeWriter.WriteLine(";");
            }
            if (node.AppendLineDefaultAndHidden)
            {
                Context.CodeWriter.WriteLine("#line default");
                Context.CodeWriter.WriteLine("#line hidden");
            }
        }
        else
        {
            Context.CodeWriter.WriteUsing(node.Content);

            if (node.AppendLineDefaultAndHidden)
            {
                Context.CodeWriter.WriteLine("#line default");
                Context.CodeWriter.WriteLine("#line hidden");
            }
        }
    }

    public override void WriteCSharpExpression(CSharpExpressionIntermediateNode node)
    {
        Context.CodeWriter.WriteStartMethodInvocation(WriteCSharpExpressionMethod);
        Context.CodeWriter.WriteLine();
        WriteCSharpChildren(node.Children);
        Context.CodeWriter.WriteEndMethodInvocation();
    }

    public override void WriteCSharpCode(CSharpCodeIntermediateNode node)
    {
        var isWhitespaceStatement = true;
        for (var i = 0; i < node.Children.Count; i++)
        {
            var token = node.Children[i] as IntermediateToken;
            if (token == null || !string.IsNullOrWhiteSpace(token.Content))
            {
                isWhitespaceStatement = false;
                break;
            }
        }

        if (isWhitespaceStatement)
        {
            return;
        }

        WriteCSharpChildren(node.Children);
        Context.CodeWriter.WriteLine();
    }

    private void WriteCSharpChildren(IntermediateNodeCollection children)
    {
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i] is CSharpIntermediateToken token)
            {
                using (Context.CodeWriter.BuildEnhancedLinePragma(token.Source, Context))
                {
                    Context.CodeWriter.Write(token.Content);
                }
            }
            else
            {
                // There may be something else inside the statement like an extension node.
                Context.RenderNode(children[i]);
            }
        }
    }

    public override void WriteHtmlAttribute(HtmlAttributeIntermediateNode node)
    {
        var valuePieceCount = node
            .Children
            .Count(child =>
                child is HtmlAttributeValueIntermediateNode ||
                child is CSharpExpressionAttributeValueIntermediateNode ||
                child is CSharpCodeAttributeValueIntermediateNode ||
                child is ExtensionIntermediateNode);

        var source = node.Source.AssumeNotNull();
        var prefixLocation = source.AbsoluteIndex;
        var suffixLocation = source.AbsoluteIndex + source.Length - node.Suffix.Length;

        Context.CodeWriter
            .WriteStartMethodInvocation(BeginWriteAttributeMethod)
            .WriteStringLiteral(node.AttributeName)
            .WriteParameterSeparator()
            .WriteStringLiteral(node.Prefix)
            .WriteParameterSeparator()
            .Write(prefixLocation.ToString(CultureInfo.InvariantCulture))
            .WriteParameterSeparator()
            .WriteStringLiteral(node.Suffix)
            .WriteParameterSeparator()
            .Write(suffixLocation.ToString(CultureInfo.InvariantCulture))
            .WriteParameterSeparator()
            .Write(valuePieceCount.ToString(CultureInfo.InvariantCulture))
            .WriteEndMethodInvocation();

        Context.RenderChildren(node);

        Context.CodeWriter
            .WriteStartMethodInvocation(EndWriteAttributeMethod)
            .WriteEndMethodInvocation();
    }

    public override void WriteHtmlAttributeValue(HtmlAttributeValueIntermediateNode node)
    {
        var source = node.Source.AssumeNotNull();
        var prefixLocation = source.AbsoluteIndex;
        var valueLocation = source.AbsoluteIndex + node.Prefix.Length;
        var valueLength = source.Length;

        Context.CodeWriter
            .WriteStartMethodInvocation(WriteAttributeValueMethod)
            .WriteStringLiteral(node.Prefix)
            .WriteParameterSeparator()
            .Write(prefixLocation.ToString(CultureInfo.InvariantCulture))
            .WriteParameterSeparator();

        // Write content
        for (var i = 0; i < node.Children.Count; i++)
        {
            if (node.Children[i] is HtmlIntermediateToken token)
            {
                Context.CodeWriter.WriteStringLiteral(token.Content);
            }
            else
            {
                // There may be something else inside the attribute value like an extension node.
                Context.RenderNode(node.Children[i]);
            }
        }

        Context.CodeWriter
            .WriteParameterSeparator()
            .Write(valueLocation.ToString(CultureInfo.InvariantCulture))
            .WriteParameterSeparator()
            .Write(valueLength.ToString(CultureInfo.InvariantCulture))
            .WriteParameterSeparator()
            .WriteBooleanLiteral(true)
            .WriteEndMethodInvocation();
    }

    public override void WriteCSharpExpressionAttributeValue(CSharpExpressionAttributeValueIntermediateNode node)
    {
        var source = node.Source.AssumeNotNull();
        var prefixLocation = source.AbsoluteIndex.ToString(CultureInfo.InvariantCulture);

        Context.CodeWriter
            .WriteStartMethodInvocation(WriteAttributeValueMethod)
            .WriteStringLiteral(node.Prefix)
            .WriteParameterSeparator()
            .Write(prefixLocation)
            .WriteParameterSeparator();

        WriteCSharpChildren(node.Children);

        var valueLocation = source.AbsoluteIndex + node.Prefix.Length;
        var valueLength = source.Length - node.Prefix.Length;

        Context.CodeWriter
            .WriteParameterSeparator()
            .Write(valueLocation.ToString(CultureInfo.InvariantCulture))
            .WriteParameterSeparator()
            .Write(valueLength.ToString(CultureInfo.InvariantCulture))
            .WriteParameterSeparator()
            .WriteBooleanLiteral(false)
            .WriteEndMethodInvocation();
    }

    public override void WriteCSharpCodeAttributeValue(CSharpCodeAttributeValueIntermediateNode node)
    {
        const string ValueWriterName = "__razor_attribute_value_writer";

        var source = node.Source.AssumeNotNull();
        var prefixLocation = source.AbsoluteIndex;
        var valueLocation = source.AbsoluteIndex + node.Prefix.Length;
        var valueLength = source.Length - node.Prefix.Length;

        Context.CodeWriter
            .WriteStartMethodInvocation(WriteAttributeValueMethod)
            .WriteStringLiteral(node.Prefix)
            .WriteParameterSeparator()
            .Write(prefixLocation.ToString(CultureInfo.InvariantCulture))
            .WriteParameterSeparator();

        Context.CodeWriter.WriteStartNewObject(TemplateTypeName);

        using (Context.CodeWriter.BuildAsyncLambda(ValueWriterName))
        {
            BeginWriterScope(ValueWriterName);
            WriteCSharpChildren(node.Children);
            EndWriterScope();
        }

        Context.CodeWriter.WriteEndMethodInvocation(false);

        Context.CodeWriter
            .WriteParameterSeparator()
            .Write(valueLocation.ToString(CultureInfo.InvariantCulture))
            .WriteParameterSeparator()
            .Write(valueLength.ToString(CultureInfo.InvariantCulture))
            .WriteParameterSeparator()
            .WriteBooleanLiteral(false)
            .WriteEndMethodInvocation();
    }

    public override void WriteHtmlContent(HtmlContentIntermediateNode node)
    {
        const int MaxStringLiteralLength = 1024;

        using var htmlContentBuilder = new PooledArrayBuilder<ReadOnlyMemory<char>>();

        var length = 0;
        foreach (var child in node.Children)
        {
            if (child is HtmlIntermediateToken token)
            {
                var htmlContent = token.Content.AsMemory();

                htmlContentBuilder.Add(htmlContent);
                length += htmlContent.Length;
            }
        }

        // Can't use a pooled builder here as the memory will be stored in the context.
        var content = new char[length];
        var contentIndex = 0;
        foreach (var htmlContent in htmlContentBuilder)
        {
            htmlContent.Span.CopyTo(content.AsSpan(contentIndex));
            contentIndex += htmlContent.Length;
        }

        WriteHtmlLiteral(Context, MaxStringLiteralLength, content.AsMemory());
    }

    // Internal for testing
    internal void WriteHtmlLiteral(CodeRenderingContext context, int maxStringLiteralLength, ReadOnlyMemory<char> literal)
    {
        while (literal.Length > maxStringLiteralLength)
        {
            // String is too large, render the string in pieces to avoid Roslyn OOM exceptions at compile time: https://github.com/aspnet/External/issues/54
            var lastCharBeforeSplit = literal.Span[maxStringLiteralLength - 1];

            // If character at splitting point is a high surrogate, take one less character this iteration
            // as we're attempting to split a surrogate pair. This can happen when something like an
            // emoji sits on the barrier between splits; if we were to split the emoji we'd end up with
            // invalid bytes in our output.
            var renderCharCount = char.IsHighSurrogate(lastCharBeforeSplit) ? maxStringLiteralLength - 1 : maxStringLiteralLength;

            WriteLiteral(literal[..renderCharCount]);

            literal = literal[renderCharCount..];
        }

        WriteLiteral(literal);
        return;

        void WriteLiteral(ReadOnlyMemory<char> content)
        {
            context.CodeWriter
                .WriteStartMethodInvocation(WriteHtmlContentMethod)
                .WriteStringLiteral(content)
                .WriteEndMethodInvocation();
        }
    }

    public override void BeginWriterScope(string writer)
    {
        Context.CodeWriter.WriteMethodInvocation(PushWriterMethod, writer);
    }

    public override void EndWriterScope()
    {
        Context.CodeWriter.WriteMethodInvocation(PopWriterMethod);
    }
}
