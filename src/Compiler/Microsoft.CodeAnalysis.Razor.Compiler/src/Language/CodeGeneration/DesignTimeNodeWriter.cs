﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

public class DesignTimeNodeWriter : IntermediateNodeWriter
{
    public override void WriteUsingDirective(CodeRenderingContext context, UsingDirectiveIntermediateNode node)
    {
        if (node.Source is { FilePath: not null } source)
        {
            using (context.BuildLinePragma(source, suppressLineDefaultAndHidden: !node.AppendLineDefaultAndHidden))
            {
                context.AddSourceMappingFor(node);
                context.CodeWriter.WriteUsing(node.Content);
            }
        }
        else
        {
            context.CodeWriter.WriteUsing(node.Content);

            if (node.AppendLineDefaultAndHidden)
            {
                context.CodeWriter.WriteLine("#line default");
                context.CodeWriter.WriteLine("#line hidden");
            }
        }
    }

    public override void WriteCSharpExpression(CodeRenderingContext context, CSharpExpressionIntermediateNode node)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (node.Children.Count == 0)
        {
            return;
        }

        if (node.Source is SourceSpan source)
        {
            using (context.BuildLinePragma(source))
            {
                var offset = DesignTimeDirectivePass.DesignTimeVariable.Length + " = ".Length;
                context.CodeWriter.WritePadding(offset, source, context);
                context.CodeWriter.WriteStartAssignment(DesignTimeDirectivePass.DesignTimeVariable);

                foreach (var child in node.Children)
                {
                    if (child is IntermediateToken { IsCSharp: true } token)
                    {
                        context.AddSourceMappingFor(token);
                        context.CodeWriter.Write(token.Content);
                    }
                    else
                    {
                        // There may be something else inside the expression like a Template or another extension node.
                        context.RenderNode(child);
                    }
                }

                context.CodeWriter.WriteLine(";");
            }
        }
        else
        {
            context.CodeWriter.WriteStartAssignment(DesignTimeDirectivePass.DesignTimeVariable);
            for (var i = 0; i < node.Children.Count; i++)
            {
                if (node.Children[i] is IntermediateToken token && token.IsCSharp)
                {
                    context.CodeWriter.Write(token.Content);
                }
                else
                {
                    // There may be something else inside the expression like a Template or another extension node.
                    context.RenderNode(node.Children[i]);
                }
            }
            context.CodeWriter.WriteLine(";");
        }
    }

    public override void WriteCSharpCode(CodeRenderingContext context, CSharpCodeIntermediateNode node)
    {
        var writer = context.CodeWriter;

        if (node.Source is SourceSpan source)
        {
            using (context.BuildLinePragma(source))
            {
                writer.WritePadding(0, source, context);
                RenderChildren(context, node);
            }
        }
        else
        {
            RenderChildren(context, node);
            writer.WriteLine();
        }

        static void RenderChildren(CodeRenderingContext context, CSharpCodeIntermediateNode node)
        {
            foreach (var child in node.Children)
            {
                if (child is IntermediateToken { IsCSharp: true } token)
                {
                    context.AddSourceMappingFor(token);
                    context.CodeWriter.Write(token.Content);
                }
                else
                {
                    // There may be something else inside the statement like an extension node.
                    context.RenderNode(child);
                }
            }
        }
    }

    public override void WriteHtmlAttribute(CodeRenderingContext context, HtmlAttributeIntermediateNode node)
    {
        context.RenderChildren(node);
    }

    public override void WriteHtmlAttributeValue(CodeRenderingContext context, HtmlAttributeValueIntermediateNode node)
    {
        context.RenderChildren(node);
    }

    public override void WriteCSharpExpressionAttributeValue(CodeRenderingContext context, CSharpExpressionAttributeValueIntermediateNode node)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (node.Children.Count == 0)
        {
            return;
        }

        var firstChild = node.Children[0];
        if (firstChild.Source is SourceSpan source)
        {
            using (context.BuildLinePragma(source))
            {
                var offset = DesignTimeDirectivePass.DesignTimeVariable.Length + " = ".Length;
                context.CodeWriter.WritePadding(offset, source, context);
                context.CodeWriter.WriteStartAssignment(DesignTimeDirectivePass.DesignTimeVariable);

                for (var i = 0; i < node.Children.Count; i++)
                {
                    if (node.Children[i] is IntermediateToken token && token.IsCSharp)
                    {
                        context.AddSourceMappingFor(token);
                        context.CodeWriter.Write(token.Content);
                    }
                    else
                    {
                        // There may be something else inside the expression like a Template or another extension node.
                        context.RenderNode(node.Children[i]);
                    }
                }

                context.CodeWriter.WriteLine(";");
            }
        }
        else
        {
            context.CodeWriter.WriteStartAssignment(DesignTimeDirectivePass.DesignTimeVariable);
            for (var i = 0; i < node.Children.Count; i++)
            {
                if (node.Children[i] is IntermediateToken token && token.IsCSharp)
                {
                    if (token.Source != null)
                    {
                        context.AddSourceMappingFor(token);
                    }

                    context.CodeWriter.Write(token.Content);
                }
                else
                {
                    // There may be something else inside the expression like a Template or another extension node.
                    context.RenderNode(node.Children[i]);
                }
            }
            context.CodeWriter.WriteLine(";");
        }
    }

    public override void WriteCSharpCodeAttributeValue(CodeRenderingContext context, CSharpCodeAttributeValueIntermediateNode node)
    {
        foreach (var child in node.Children)
        {
            if (child is IntermediateToken { IsCSharp: true } token)
            {
                var isWhitespaceStatement = string.IsNullOrWhiteSpace(token.Content);

                if (token.Source is SourceSpan source)
                {
                    if (!isWhitespaceStatement)
                    {
                        using (context.BuildLinePragma(source))
                        {
                            context.CodeWriter.WritePadding(0, source, context);

                            context.AddSourceMappingFor(token);
                            context.CodeWriter.Write(token.Content);
                        }

                        continue;
                    }

                    context.CodeWriter.WritePadding(0, source, context);
                }
                else if (isWhitespaceStatement)
                {
                    // Don't write whitespace if there is no line mapping for it.
                    continue;
                }

                context.AddSourceMappingFor(token);
                context.CodeWriter.Write(token.Content);
                context.CodeWriter.WriteLine();
            }
            else
            {
                // There may be something else inside the statement like an extension node.
                context.RenderNode(child);
            }
        }
    }

    public override void WriteHtmlContent(CodeRenderingContext context, HtmlContentIntermediateNode node)
    {
        // Do nothing
    }

    public override void BeginWriterScope(CodeRenderingContext context, string writer)
    {
        // Do nothing
    }

    public override void EndWriterScope(CodeRenderingContext context)
    {
        // Do nothing
    }
}
