// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions.Version2_X;

internal sealed class ViewComponentTagHelperTargetExtension : IViewComponentTagHelperTargetExtension
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
            context.CodeWriter.WriteFieldDeclaration(
                CommonModifiers.PrivateReadOnly,
                ViewComponentsApi.IViewComponentHelper.GloballyQualifiedTypeName,
                ViewComponentHelperVariableName);

            // Add constructor.
            WriteConstructorString(context.CodeWriter, node.ClassName);

            // Add attributes.
            WriteAttributeDeclarations(context.CodeWriter, node.TagHelper);

            // Add process method.
            WriteProcessMethodString(context.CodeWriter, node.TagHelper);
        }
    }

    private static void WriteConstructorString(CodeWriter writer, string className)
    {
        using (writer.BuildConstructorDeclaration(
            CommonModifiers.Public,
            className,
            [(ViewComponentsApi.IViewComponentHelper.GloballyQualifiedTypeName, "helper")]))
        {
            writer.WriteStartAssignment(ViewComponentHelperVariableName)
                .WriteLine("helper;");
        }
    }

    private static void WriteAttributeDeclarations(CodeWriter writer, TagHelperDescriptor tagHelper)
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

    private static void WriteProcessMethodString(CodeWriter writer, TagHelperDescriptor tagHelper)
    {
        using (writer.BuildMethodDeclaration(
            CommonModifiers.PublicOverrideAsync,
            $"global::{typeof(Task).FullName}",
            ViewComponentsApi.ProcessAsyncMethodName,
            parameters: [
                (ViewComponentsApi.TagHelperContext.FullTypeName, TagHelperContextVariableName),
                (ViewComponentsApi.TagHelperOutput.FullTypeName, TagHelperOutputVariableName)
            ]))
        {
            writer.WriteInstanceMethodInvocation(
                $"({ViewComponentHelperVariableName} as {ViewComponentsApi.IViewContextAware.GloballyQualifiedTypeName})?",
                ViewComponentsApi.IViewContextAware.ContextualizeMethodName,
                arguments: [ViewComponentsApi.ViewContextPropertyName]);

            var methodArguments = GetMethodArguments(tagHelper);
            writer.Write("var ")
                .WriteStartAssignment(TagHelperContentVariableName)
                .Write("await ")
                .WriteInstanceMethodInvocation(ViewComponentHelperVariableName, ViewComponentsApi.IViewComponentHelper.InvokeMethodName, methodArguments);
            writer.WriteStartAssignment($"{TagHelperOutputVariableName}.{ViewComponentsApi.TagHelperOutput.TagNamePropertyName}")
                .WriteLine("null;");
            writer.WriteInstanceMethodInvocation(
                $"{TagHelperOutputVariableName}.{ViewComponentsApi.TagHelperOutput.ContentPropertyName}",
                ViewComponentsApi.TagHelperOutput.ContentSetMethodName,
                arguments: [TagHelperContentVariableName]);
        }
    }

    private static ImmutableArray<string> GetMethodArguments(TagHelperDescriptor tagHelper)
    {
        var propertyNames = tagHelper.BoundAttributes.Select(attribute => attribute.PropertyName);
        var joinedPropertyNames = string.Join(", ", propertyNames);
        var parametersString = $"new {{ { joinedPropertyNames } }}";
        var viewComponentName = tagHelper.GetViewComponentName();

        return [$"\"{viewComponentName}\"", parametersString];
    }

    private static void WriteTargetElementString(CodeWriter writer, TagHelperDescriptor tagHelper)
    {
        Debug.Assert(tagHelper.TagMatchingRules.Length == 1);

        var rule = tagHelper.TagMatchingRules[0];

        writer.Write("[")
            .WriteStartMethodInvocation(ViewComponentsApi.HtmlTargetElementAttribute.FullTypeName)
            .WriteStringLiteral(rule.TagName)
            .WriteLine(")]");
    }
}
