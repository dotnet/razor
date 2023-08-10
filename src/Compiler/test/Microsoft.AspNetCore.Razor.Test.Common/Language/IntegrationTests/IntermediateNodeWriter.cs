﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.IntegrationTests;

// Serializes single IR nodes (shallow).
public class IntermediateNodeWriter :
    IntermediateNodeVisitor,
    IExtensionIntermediateNodeVisitor<SectionIntermediateNode>,
    IExtensionIntermediateNodeVisitor<RouteAttributeExtensionNode>

{
    private readonly TextWriter _writer;

    public IntermediateNodeWriter(TextWriter writer)
    {
        _writer = writer;
    }

    public int Depth { get; set; }

    public override void VisitDefault(IntermediateNode node)
    {
        WriteBasicNode(node);
    }

    public override void VisitClassDeclaration(ClassDeclarationIntermediateNode node)
    {
        var entries = new List<string>()
            {
                string.Join(" ", node.Modifiers),
                node.ClassName,
                node.BaseType,
                string.Join(", ", node.Interfaces ?? Array.Empty<string>())
            };

        // Avoid adding the type parameters to the baseline if they aren't present.
        if (node.TypeParameters != null && node.TypeParameters.Count > 0)
        {
            entries.Add(string.Join(", ", node.TypeParameters.Select(p => p.ParameterName)));
        }

        WriteContentNode(node, entries.ToArray());
    }

    public override void VisitCSharpExpressionAttributeValue(CSharpExpressionAttributeValueIntermediateNode node)
    {
        WriteContentNode(node, node.Prefix);
    }

    public override void VisitCSharpCodeAttributeValue(CSharpCodeAttributeValueIntermediateNode node)
    {
        WriteContentNode(node, node.Prefix);
    }

    public override void VisitToken(IntermediateToken node)
    {
        WriteContentNode(node, node.Kind.ToString(), node.Content);
    }

    public override void VisitMalformedDirective(MalformedDirectiveIntermediateNode node)
    {
        WriteContentNode(node, node.DirectiveName);
    }

    public override void VisitDirective(DirectiveIntermediateNode node)
    {
        WriteContentNode(node, node.DirectiveName);
    }

    public override void VisitDirectiveToken(DirectiveTokenIntermediateNode node)
    {
        WriteContentNode(node, node.Content);
    }

    public override void VisitFieldDeclaration(FieldDeclarationIntermediateNode node)
    {
        WriteContentNode(node, string.Join(" ", node.Modifiers), node.FieldType, node.FieldName);
    }

    public override void VisitHtmlAttribute(HtmlAttributeIntermediateNode node)
    {
        WriteContentNode(node, node.Prefix, node.Suffix);
    }

    public override void VisitHtmlAttributeValue(HtmlAttributeValueIntermediateNode node)
    {
        WriteContentNode(node, node.Prefix);
    }

    public override void VisitNamespaceDeclaration(NamespaceDeclarationIntermediateNode node)
    {
        WriteContentNode(node, node.Content);
    }

    public override void VisitMethodDeclaration(MethodDeclarationIntermediateNode node)
    {
        WriteContentNode(node, string.Join(" ", node.Modifiers), node.ReturnType, node.MethodName);
    }

    public override void VisitUsingDirective(UsingDirectiveIntermediateNode node)
    {
        WriteContentNode(node, node.Content);
    }

    public override void VisitTagHelper(TagHelperIntermediateNode node)
    {
        WriteContentNode(node, node.TagName, string.Format(CultureInfo.InvariantCulture, "{0}.{1}", nameof(TagMode), node.TagMode));
    }

    public override void VisitTagHelperProperty(TagHelperPropertyIntermediateNode node)
    {
        WriteContentNode(node, node.AttributeName, node.BoundAttribute.DisplayName, string.Format(CultureInfo.InvariantCulture, "HtmlAttributeValueStyle.{0}", node.AttributeStructure));
    }

    public override void VisitTagHelperHtmlAttribute(TagHelperHtmlAttributeIntermediateNode node)
    {
        WriteContentNode(node, node.AttributeName, string.Format(CultureInfo.InvariantCulture, "HtmlAttributeValueStyle.{0}", node.AttributeStructure));
    }

    public override void VisitTagHelperDirectiveAttribute(TagHelperDirectiveAttributeIntermediateNode node)
    {
        WriteContentNode(node, node.AttributeName, node.BoundAttribute.DisplayName, string.Format(CultureInfo.InvariantCulture, "HtmlAttributeValueStyle.{0}", node.AttributeStructure));
    }

    public override void VisitTagHelperDirectiveAttributeParameter(TagHelperDirectiveAttributeParameterIntermediateNode node)
    {
        WriteContentNode(node, node.AttributeName, string.Format(CultureInfo.InvariantCulture, "HtmlAttributeValueStyle.{0}", node.AttributeStructure));
    }

    public override void VisitComponent(ComponentIntermediateNode node)
    {
        WriteContentNode(node, node.TagName);
    }

    public override void VisitComponentAttribute(ComponentAttributeIntermediateNode node)
    {
        WriteContentNode(node, node.AttributeName, node.PropertyName, string.Format(CultureInfo.InvariantCulture, "AttributeStructure.{0}", node.AttributeStructure));
    }

    public override void VisitComponentChildContent(ComponentChildContentIntermediateNode node)
    {
        WriteContentNode(node, node.AttributeName, node.ParameterName);
    }

    public override void VisitComponentTypeArgument(ComponentTypeArgumentIntermediateNode node)
    {
        WriteContentNode(node, node.TypeParameterName);
    }

    public override void VisitComponentTypeInferenceMethod(ComponentTypeInferenceMethodIntermediateNode node)
    {
        WriteContentNode(node, node.FullTypeName, node.MethodName);
    }

    public override void VisitMarkupElement(MarkupElementIntermediateNode node)
    {
        WriteContentNode(node, node.TagName);
    }

    public override void VisitMarkupBlock(MarkupBlockIntermediateNode node)
    {
        WriteContentNode(node, node.Content);
    }

    public override void VisitReferenceCapture(ReferenceCaptureIntermediateNode node)
    {
        WriteContentNode(node, node.IdentifierToken?.Content);
    }

    public override void VisitSetKey(SetKeyIntermediateNode node)
    {
        WriteContentNode(node, node.KeyValueToken?.Content);
    }

    void IExtensionIntermediateNodeVisitor<RouteAttributeExtensionNode>.VisitExtension(RouteAttributeExtensionNode node)
    {
        WriteContentNode(node, node.Template.ToString());
    }

    public override void VisitExtension(ExtensionIntermediateNode node)
    {
        switch (node)
        {
            case PreallocatedTagHelperHtmlAttributeIntermediateNode n:
                WriteContentNode(n, n.VariableName);
                break;
            case PreallocatedTagHelperHtmlAttributeValueIntermediateNode n:
                WriteContentNode(n, n.VariableName, n.AttributeName, n.Value, string.Format(CultureInfo.InvariantCulture, "HtmlAttributeValueStyle.{0}", n.AttributeStructure));
                break;
            case PreallocatedTagHelperPropertyIntermediateNode n:
                WriteContentNode(n, n.VariableName, n.AttributeName, n.PropertyName);
                break;
            case PreallocatedTagHelperPropertyValueIntermediateNode n:
                WriteContentNode(n, n.VariableName, n.AttributeName, n.Value, string.Format(CultureInfo.InvariantCulture, "HtmlAttributeValueStyle.{0}", n.AttributeStructure));
                break;
            case DefaultTagHelperCreateIntermediateNode n:
                WriteContentNode(n, n.TypeName);
                break;
            case DefaultTagHelperExecuteIntermediateNode n:
                WriteBasicNode(n);
                break;
            case DefaultTagHelperHtmlAttributeIntermediateNode n:
                WriteContentNode(n, n.AttributeName, string.Format(CultureInfo.InvariantCulture, "HtmlAttributeValueStyle.{0}", n.AttributeStructure));
                break;
            case DefaultTagHelperPropertyIntermediateNode n:
                WriteContentNode(n, n.AttributeName, n.BoundAttribute.DisplayName, string.Format(CultureInfo.InvariantCulture, "HtmlAttributeValueStyle.{0}", n.AttributeStructure));
                break;
            case DefaultTagHelperRuntimeIntermediateNode n:
                WriteBasicNode(n);
                break;
            default:
                base.VisitExtension(node);
                break;
        }
    }

    public void VisitExtension(SectionIntermediateNode node)
    {
        WriteContentNode(node, node.SectionName);
    }

    protected void WriteBasicNode(IntermediateNode node)
    {
        WriteIndent();
        WriteName(node);
        WriteSeparator();
        WriteSourceRange(node);
    }

    protected void WriteContentNode(IntermediateNode node, params string[] content)
    {
        WriteIndent();
        WriteName(node);
        WriteSeparator();
        WriteSourceRange(node);

        for (var i = 0; i < content.Length; i++)
        {
            WriteSeparator();
            WriteContent(content[i]);
        }
    }

    protected void WriteIndent()
    {
        for (var i = 0; i < Depth; i++)
        {
            for (var j = 0; j < 4; j++)
            {
                _writer.Write(' ');
            }
        }
    }

    protected void WriteSeparator()
    {
        _writer.Write(" - ");
    }

    protected void WriteNewLine()
    {
        _writer.WriteLine();
    }

    protected void WriteName(IntermediateNode node)
    {
        var typeName = node.GetType().Name;
        if (typeName.EndsWith("IntermediateNode", StringComparison.Ordinal))
        {
            _writer.Write(typeName.Substring(0, typeName.Length - "IntermediateNode".Length));
        }
        else
        {
            _writer.Write(typeName);
        }
    }

    protected void WriteSourceRange(IntermediateNode node)
    {
        if (node.Source != null)
        {
            WriteSourceRange(node.Source.Value);
        }
    }

    protected void WriteSourceRange(SourceSpan sourceRange)
    {
        _writer.Write("(");
        _writer.Write(sourceRange.AbsoluteIndex);
        _writer.Write(":");
        _writer.Write(sourceRange.LineIndex);
        _writer.Write(",");
        _writer.Write(sourceRange.CharacterIndex);
        _writer.Write(" [");
        _writer.Write(sourceRange.Length);
        _writer.Write("] ");

        if (sourceRange.FilePath != null)
        {
            var fileName = sourceRange.FilePath.Substring(sourceRange.FilePath.LastIndexOf('/') + 1);
            _writer.Write(fileName);
        }

        _writer.Write(")");
    }

    protected void WriteDiagnostics(IntermediateNode node)
    {
        if (node.HasDiagnostics)
        {
            _writer.Write("| ");
            for (var i = 0; i < node.Diagnostics.Count; i++)
            {
                var diagnostic = node.Diagnostics[i];
                _writer.Write("{");
                WriteSourceRange(diagnostic.Span);
                _writer.Write(": ");
                _writer.Write(diagnostic.Severity);
                _writer.Write(" ");
                _writer.Write(diagnostic.Id);
                _writer.Write(": ");

                // Purposefully not writing out the entire message to ensure readable IR and because messages
                // can span multiple lines. Not using string.GetHashCode because we can't have any collisions.
                using (var hashAlgorithm = HashAlgorithmOperations.Create())
                {
                    var diagnosticMessage = diagnostic.GetMessage(CultureInfo.InvariantCulture);
                    var messageBytes = Encoding.UTF8.GetBytes(diagnosticMessage);
                    var messageHash = hashAlgorithm.ComputeHash(messageBytes);
                    var stringHashBuilder = new StringBuilder();

                    for (var j = 0; j < messageHash.Length; j++)
                    {
                        stringHashBuilder.Append(messageHash[j].ToString("x2", CultureInfo.InvariantCulture));
                    }

                    var stringHash = stringHashBuilder.ToString();
                    _writer.Write(stringHash);
                }
                _writer.Write("} ");
            }
        }
    }

    protected void WriteContent(string content)
    {
        if (content == null)
        {
            return;
        }

        // We explicitly escape newlines in node content so that the IR can be compared line-by-line. The escaped
        // newline cannot be platform specific so we need to drop the windows \r.
        // Also, escape our separator so we can search for ` - `to find delimiters.
        _writer.Write(content.Replace("\r", string.Empty).Replace("\n", "\\n").Replace(" - ", "\\-"));
    }
}
