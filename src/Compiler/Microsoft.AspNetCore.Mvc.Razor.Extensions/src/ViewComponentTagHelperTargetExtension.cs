﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions;

internal class ViewComponentTagHelperTargetExtension : IViewComponentTagHelperTargetExtension
{
    private static readonly string[] PublicModifiers = new[] { "public" };

    public string TagHelperTypeName { get; set; } = "Microsoft.AspNetCore.Razor.TagHelpers.TagHelper";

    public string ViewComponentHelperTypeName { get; set; } = "global::Microsoft.AspNetCore.Mvc.IViewComponentHelper";

    public string ViewComponentHelperVariableName { get; set; } = "__helper";

    public string ViewComponentInvokeMethodName { get; set; } = "InvokeAsync";

    public string HtmlAttributeNotBoundAttributeTypeName { get; set; } = "Microsoft.AspNetCore.Razor.TagHelpers.HtmlAttributeNotBoundAttribute";

    public string ViewContextAttributeTypeName { get; set; } = "global::Microsoft.AspNetCore.Mvc.ViewFeatures.ViewContextAttribute";

    public string ViewContextTypeName { get; set; } = "global::Microsoft.AspNetCore.Mvc.Rendering.ViewContext";

    public string ViewContextPropertyName { get; set; } = "ViewContext";

    public string HtmlTargetElementAttributeTypeName { get; set; } = "Microsoft.AspNetCore.Razor.TagHelpers.HtmlTargetElementAttribute";

    public string TagHelperProcessMethodName { get; set; } = "ProcessAsync";

    public string TagHelperProcessInvokeAsyncArgsMethodName { get; set; } = "ProcessInvokeAsyncArgs";

    public string TagHelperContextTypeName { get; set; } = "Microsoft.AspNetCore.Razor.TagHelpers.TagHelperContext";

    public string TagHelperContextVariableName { get; set; } = "__context";

    public string TagHelperContextAttributesVariableName { get; set; } = "AllAttributes";

    public string TagHelperOutputTypeName { get; set; } = "Microsoft.AspNetCore.Razor.TagHelpers.TagHelperOutput";

    public string TagHelperOutputVariableName { get; set; } = "__output";

    public string TagHelperOutputTagNamePropertyName { get; set; } = "TagName";

    public string TagHelperOutputContentPropertyName { get; set; } = "Content";

    public string TagHelperContentSetMethodName { get; set; } = "SetHtmlContent";

    public string TagHelperContentVariableName { get; set; } = "__helperContent";

    public string IViewContextAwareTypeName { get; set; } = "global::Microsoft.AspNetCore.Mvc.ViewFeatures.IViewContextAware";

    public string IViewContextAwareContextualizeMethodName { get; set; } = "Contextualize";

    public void WriteViewComponentTagHelper(CodeRenderingContext context, ViewComponentTagHelperIntermediateNode node)
    {
        // Add target element.
        WriteTargetElementString(context.CodeWriter, node.TagHelper);

        // Initialize declaration.
        using (context.CodeWriter.BuildClassDeclaration(
            PublicModifiers,
            node.ClassName,
            TagHelperTypeName,
            interfaces: null,
            typeParameters: null,
            context))
        {
            // Add view component helper.
            context.CodeWriter.WriteVariableDeclaration(
                $"private readonly {ViewComponentHelperTypeName}",
                ViewComponentHelperVariableName,
                value: null);

            // Add constructor.
            WriteConstructorString(context.CodeWriter, node.ClassName);

            // Add attributes.
            WriteAttributeDeclarations(context.CodeWriter, node.TagHelper);

            // Add process method.
            WriteProcessMethodString(context.CodeWriter, node.TagHelper);

            // We pre-process the arguments passed to `InvokeAsync` to ensure that the
            // provided markup attributes (in kebab-case) are matched to the associated
            // properties in the VCTH class.
            WriteProcessInvokeAsyncArgsMethodString(context.CodeWriter, node.TagHelper);
        }
    }

    private void WriteConstructorString(CodeWriter writer, string className)
    {
        writer.Write("public ")
            .Write(className)
            .Write("(")
            .Write($"{ViewComponentHelperTypeName} helper")
            .WriteLine(")");
        using (writer.BuildScope())
        {
            writer.WriteStartAssignment(ViewComponentHelperVariableName)
                .Write("helper")
                .WriteLine(";");
        }
    }

    private void WriteAttributeDeclarations(CodeWriter writer, TagHelperDescriptor tagHelper)
    {
        writer.Write("[")
          .Write(HtmlAttributeNotBoundAttributeTypeName)
          .WriteParameterSeparator()
          .Write(ViewContextAttributeTypeName)
          .WriteLine("]");

        writer.WriteAutoPropertyDeclaration(
            PublicModifiers,
            ViewContextTypeName,
            ViewContextPropertyName);

        foreach (var attribute in tagHelper.BoundAttributes)
        {
            writer.WriteAutoPropertyDeclaration(
                PublicModifiers,
                attribute.TypeName,
                attribute.GetPropertyName());

            if (attribute.IndexerTypeName != null)
            {
                writer.Write(" = ")
                    .WriteStartNewObject(attribute.TypeName)
                    .WriteEndMethodInvocation();
            }
        }
    }

    private void WriteProcessMethodString(CodeWriter writer, TagHelperDescriptor tagHelper)
    {
        using (writer.BuildMethodDeclaration(
                $"public override async",
                $"global::{typeof(Task).FullName}",
                TagHelperProcessMethodName,
                new Dictionary<string, string>()
                {
                        { TagHelperContextTypeName, TagHelperContextVariableName },
                        { TagHelperOutputTypeName, TagHelperOutputVariableName }
                }))
        {
            writer.WriteInstanceMethodInvocation(
                $"({ViewComponentHelperVariableName} as {IViewContextAwareTypeName})?",
                IViewContextAwareContextualizeMethodName,
                new[] { ViewContextPropertyName });

            var methodParameters = GetMethodParameters(tagHelper);
            writer.Write("var ")
                .WriteStartAssignment(TagHelperContentVariableName)
                .WriteInstanceMethodInvocation($"await {ViewComponentHelperVariableName}", ViewComponentInvokeMethodName, methodParameters);
            writer.WriteStartAssignment($"{TagHelperOutputVariableName}.{TagHelperOutputTagNamePropertyName}")
                .WriteLine("null;");
            writer.WriteInstanceMethodInvocation(
                $"{TagHelperOutputVariableName}.{TagHelperOutputContentPropertyName}",
                TagHelperContentSetMethodName,
                new[] { TagHelperContentVariableName });
        }
    }

    private void WriteProcessInvokeAsyncArgsMethodString(CodeWriter writer, TagHelperDescriptor tagHelper)
    {
        var methodReturnType = "Dictionary<string, object>";
        using (writer.BuildMethodDeclaration(
            "private",
            methodReturnType,
            TagHelperProcessInvokeAsyncArgsMethodName,
            new Dictionary<string, string>() { { TagHelperContextTypeName, TagHelperContextVariableName } }))
        {
            writer.WriteStartAssignment($"{methodReturnType} args")
                .WriteStartNewObject(methodReturnType)
                .WriteEndMethodInvocation();

            for (var i = 0; i < tagHelper.BoundAttributes.Count; i++)
            {
                var attributeName = tagHelper.BoundAttributes[i].Name;
                var parameterName = tagHelper.BoundAttributes[i].GetPropertyName();
                writer.WriteLine($"if (__context.AllAttributes.ContainsName(\"{attributeName}\"))");
                writer.WriteLine("{");
                writer.CurrentIndent += writer.TabSize;
                writer.WriteLine($"args[nameof({parameterName})] = {parameterName};");
                writer.CurrentIndent -= writer.TabSize;
                writer.WriteLine("}");
            }
            writer.WriteLine("return args;");
        }
    }

    private string[] GetMethodParameters(TagHelperDescriptor tagHelper)
    {
        var viewComponentName = tagHelper.GetViewComponentName();
        var methodParameters = new[] { $"\"{viewComponentName}\"", $"{TagHelperProcessInvokeAsyncArgsMethodName}({TagHelperContextVariableName})" };
        return methodParameters;
    }

    private void WriteTargetElementString(CodeWriter writer, TagHelperDescriptor tagHelper)
    {
        Debug.Assert(tagHelper.TagMatchingRules.Count == 1);

        var rule = tagHelper.TagMatchingRules[0];

        writer.Write("[")
            .WriteStartMethodInvocation(HtmlTargetElementAttributeTypeName)
            .WriteStringLiteral(rule.TagName)
            .WriteLine(")]");
    }
}
