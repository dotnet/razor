// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions.Version2_X;

internal class ViewComponentTagHelperTargetExtension : IViewComponentTagHelperTargetExtension
{
    private const string TagHelperContentVariableName = "content";
    private const string TagHelperContextVariableName = "context";
    private const string TagHelperOutputVariableName = "output";
    private const string ViewComponentHelperVariableName = "_helper";

    public void WriteViewComponentTagHelper(CodeRenderingContext context, ViewComponentTagHelperIntermediateNode node)
    {
        // Add target element.
        WriteTargetElementString(context.CodeWriter, node.TagHelper);

        // Initialize declaration.
        using (context.CodeWriter.BuildClassDeclaration(
            CommonModifiers.Public,
            node.ClassName,
            new BaseTypeWithModel(ViewComponentsApi.TagHelper.FullTypeName),
            interfaces: default,
            typeParameters: default,
            context))
        {
            // Add view component helper.
            context.CodeWriter.WriteVariableDeclaration(
                $"private readonly {ViewComponentsApi.IViewComponentHelper.GloballyQualifiedTypeName}",
                ViewComponentHelperVariableName,
                value: null);

            // Add constructor.
            WriteConstructorString(context.CodeWriter, node.ClassName);

            // Add attributes.
            WriteAttributeDeclarations(context.CodeWriter, node.TagHelper);

            // Add process method.
            WriteProcessMethodString(context.CodeWriter, node.TagHelper);
        }
    }

    private void WriteConstructorString(CodeWriter writer, string className)
    {
        writer.Write("public ")
            .Write(className)
            .Write("(")
            .Write($"{ViewComponentsApi.IViewComponentHelper.GloballyQualifiedTypeName} helper")
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
          .Write(ViewComponentsApi.HtmlAttributeNotBoundAttribute.FullTypeName)
          .WriteParameterSeparator()
          .Write(ViewComponentsApi.ViewContextAttribute.GloballyQualifiedTypeName)
          .WriteLine("]");

        writer.WriteAutoPropertyDeclaration(
            CommonModifiers.Public,
            ViewComponentsApi.ViewContext.GloballyQualifiedTypeName,
            ViewComponentsApi.ViewContextPropertyName);

        foreach (var attribute in tagHelper.BoundAttributes)
        {
            writer.WriteAutoPropertyDeclaration(
                CommonModifiers.Public,
                attribute.TypeName,
                attribute.PropertyName);

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
            ViewComponentsApi.ProcessAsyncMethodName,
            new Dictionary<string, string>()
            {
                { ViewComponentsApi.TagHelperContext.FullTypeName, TagHelperContextVariableName },
                { ViewComponentsApi.TagHelperOutput.FullTypeName, TagHelperOutputVariableName }
            }))
        {
            writer.WriteInstanceMethodInvocation(
                $"({ViewComponentHelperVariableName} as {ViewComponentsApi.IViewContextAware.GloballyQualifiedTypeName})?",
                ViewComponentsApi.IViewContextAware.ContextualizeMethodName,
                new[] { ViewComponentsApi.ViewContextPropertyName });

            var methodParameters = GetMethodParameters(tagHelper);
            writer.Write("var ")
                .WriteStartAssignment(TagHelperContentVariableName)
                .WriteInstanceMethodInvocation($"await {ViewComponentHelperVariableName}", ViewComponentsApi.IViewComponentHelper.InvokeMethodName, methodParameters);
            writer.WriteStartAssignment($"{TagHelperOutputVariableName}.{ViewComponentsApi.TagHelperOutput.TagNamePropertyName}")
                .WriteLine("null;");
            writer.WriteInstanceMethodInvocation(
                $"{TagHelperOutputVariableName}.{ViewComponentsApi.TagHelperOutput.ContentPropertyName}",
                ViewComponentsApi.TagHelperOutput.ContentSetMethodName,
                new[] { TagHelperContentVariableName });
        }
    }

    private string[] GetMethodParameters(TagHelperDescriptor tagHelper)
    {
        var propertyNames = tagHelper.BoundAttributes.Select(attribute => attribute.PropertyName);
        var joinedPropertyNames = string.Join(", ", propertyNames);
        var parametersString = $"new {{ { joinedPropertyNames } }}";
        var viewComponentName = tagHelper.GetViewComponentName();
        var methodParameters = new[] { $"\"{viewComponentName}\"", parametersString };
        return methodParameters;
    }

    private void WriteTargetElementString(CodeWriter writer, TagHelperDescriptor tagHelper)
    {
        Debug.Assert(tagHelper.TagMatchingRules.Length == 1);

        var rule = tagHelper.TagMatchingRules[0];

        writer.Write("[")
            .WriteStartMethodInvocation(ViewComponentsApi.HtmlTargetElementAttribute.FullTypeName)
            .WriteStringLiteral(rule.TagName)
            .WriteLine(")]");
    }
}
