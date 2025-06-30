// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

public class DesignTimeNodeWriter(CodeRenderingContext context) : IntermediateNodeWriter(context)
{
    public override void WriteUsingDirective(UsingDirectiveIntermediateNode node)
    {
        if (node.Source is { FilePath: not null } sourceSpan)
        {
            using (Context.CodeWriter.BuildLinePragma(sourceSpan, Context, suppressLineDefaultAndHidden: !node.AppendLineDefaultAndHidden))
            {
                Context.AddSourceMappingFor(node);
                Context.CodeWriter.WriteUsing(node.Content);
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
        if (node.Children.Count == 0)
        {
            return;
        }

        if (node.Source != null)
        {
            using (Context.CodeWriter.BuildLinePragma(node.Source.Value, Context))
            {
                var offset = DesignTimeDirectivePass.DesignTimeVariable.Length + " = ".Length;
                Context.CodeWriter.WritePadding(offset, node.Source, Context);
                Context.CodeWriter.WriteStartAssignment(DesignTimeDirectivePass.DesignTimeVariable);

                for (var i = 0; i < node.Children.Count; i++)
                {
                    if (node.Children[i] is CSharpIntermediateToken token)
                    {
                        Context.AddSourceMappingFor(token);
                        Context.CodeWriter.Write(token.Content);
                    }
                    else
                    {
                        // There may be something else inside the expression like a Template or another extension node.
                        Context.RenderNode(node.Children[i]);
                    }
                }

                Context.CodeWriter.WriteLine(";");
            }
        }
        else
        {
            Context.CodeWriter.WriteStartAssignment(DesignTimeDirectivePass.DesignTimeVariable);
            for (var i = 0; i < node.Children.Count; i++)
            {
                if (node.Children[i] is CSharpIntermediateToken token)
                {
                    Context.CodeWriter.Write(token.Content);
                }
                else
                {
                    // There may be something else inside the expression like a Template or another extension node.
                    Context.RenderNode(node.Children[i]);
                }
            }
            Context.CodeWriter.WriteLine(";");
        }
    }

    public override void WriteCSharpCode(CSharpCodeIntermediateNode node)
    {
        IDisposable? linePragmaScope = null;
        if (node.Source != null)
        {
            linePragmaScope = Context.CodeWriter.BuildLinePragma(node.Source.Value, Context);

            Context.CodeWriter.WritePadding(0, node.Source.Value, Context);
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            if (node.Children[i] is CSharpIntermediateToken token)
            {
                Context.AddSourceMappingFor(token);
                Context.CodeWriter.Write(token.Content);
            }
            else
            {
                // There may be something else inside the statement like an extension node.
                Context.RenderNode(node.Children[i]);
            }
        }

        if (linePragmaScope != null)
        {
            linePragmaScope.Dispose();
        }
        else
        {
            Context.CodeWriter.WriteLine();
        }
    }

    public override void WriteHtmlAttribute(HtmlAttributeIntermediateNode node)
    {
        Context.RenderChildren(node);
    }

    public override void WriteHtmlAttributeValue(HtmlAttributeValueIntermediateNode node)
    {
        Context.RenderChildren(node);
    }

    public override void WriteCSharpExpressionAttributeValue(CSharpExpressionAttributeValueIntermediateNode node)
    {
        if (node.Children.Count == 0)
        {
            return;
        }

        var firstChild = node.Children[0];
        if (firstChild.Source != null)
        {
            using (Context.CodeWriter.BuildLinePragma(firstChild.Source.Value, Context))
            {
                var offset = DesignTimeDirectivePass.DesignTimeVariable.Length + " = ".Length;
                Context.CodeWriter.WritePadding(offset, firstChild.Source, Context);
                Context.CodeWriter.WriteStartAssignment(DesignTimeDirectivePass.DesignTimeVariable);

                for (var i = 0; i < node.Children.Count; i++)
                {
                    if (node.Children[i] is CSharpIntermediateToken token)
                    {
                        Context.AddSourceMappingFor(token);
                        Context.CodeWriter.Write(token.Content);
                    }
                    else
                    {
                        // There may be something else inside the expression like a Template or another extension node.
                        Context.RenderNode(node.Children[i]);
                    }
                }

                Context.CodeWriter.WriteLine(";");
            }
        }
        else
        {
            Context.CodeWriter.WriteStartAssignment(DesignTimeDirectivePass.DesignTimeVariable);
            for (var i = 0; i < node.Children.Count; i++)
            {
                if (node.Children[i] is CSharpIntermediateToken token)
                {
                    if (token.Source != null)
                    {
                        Context.AddSourceMappingFor(token);
                    }

                    Context.CodeWriter.Write(token.Content);
                }
                else
                {
                    // There may be something else inside the expression like a Template or another extension node.
                    Context.RenderNode(node.Children[i]);
                }
            }

            Context.CodeWriter.WriteLine(";");
        }
    }

    public override void WriteCSharpCodeAttributeValue(CSharpCodeAttributeValueIntermediateNode node)
    {
        for (var i = 0; i < node.Children.Count; i++)
        {
            if (node.Children[i] is CSharpIntermediateToken token)
            {
                IDisposable? linePragmaScope = null;
                var isWhitespaceStatement = string.IsNullOrWhiteSpace(token.Content);

                if (token.Source != null)
                {
                    if (!isWhitespaceStatement)
                    {
                        linePragmaScope = Context.CodeWriter.BuildLinePragma(token.Source.Value, Context);
                    }

                    Context.CodeWriter.WritePadding(0, token.Source.Value, Context);
                }
                else if (isWhitespaceStatement)
                {
                    // Don't write whitespace if there is no line mapping for it.
                    continue;
                }

                Context.AddSourceMappingFor(token);
                Context.CodeWriter.Write(token.Content);

                if (linePragmaScope != null)
                {
                    linePragmaScope.Dispose();
                }
                else
                {
                    Context.CodeWriter.WriteLine();
                }
            }
            else
            {
                // There may be something else inside the statement like an extension node.
                Context.RenderNode(node.Children[i]);
            }
        }
    }

    public override void WriteHtmlContent(HtmlContentIntermediateNode node)
    {
        // Do nothing
    }

    public override void BeginWriterScope(string writer)
    {
        // Do nothing
    }

    public override void EndWriterScope()
    {
        // Do nothing
    }
}
