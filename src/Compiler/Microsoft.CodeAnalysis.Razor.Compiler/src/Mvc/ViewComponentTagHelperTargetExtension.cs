// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions;

internal sealed class ViewComponentTagHelperTargetExtension : IViewComponentTagHelperTargetExtension
{
    private const string TagHelperContentVariableName = "__helperContent";
    private const string TagHelperContextVariableName = "__context";
    private const string TagHelperOutputVariableName = "__output";
    private const string ViewComponentHelperVariableName = "__helper";

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

            // We pre-process the arguments passed to `InvokeAsync` to ensure that the
            // provided markup attributes (in kebab-case) are matched to the associated
            // properties in the VCTH class.
            WriteProcessInvokeAsyncArgsMethodString(context.CodeWriter, node.TagHelper);
        }
    }

    private static void WriteConstructorString(CodeWriter writer, string className)
    {
        using (writer.BuildConstructorDeclaration(
            CommonModifiers.Public,
            className,
            parameters: [(ViewComponentsApi.IViewComponentHelper.GloballyQualifiedTypeName, "helper")]))
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
                .WriteInstanceMethodInvocation($"await {ViewComponentHelperVariableName}", ViewComponentsApi.IViewComponentHelper.InvokeMethodName, methodArguments);
            writer.WriteStartAssignment($"{TagHelperOutputVariableName}.{ViewComponentsApi.TagHelperOutput.TagNamePropertyName}")
                .WriteLine("null;");
            writer.WriteInstanceMethodInvocation(
                $"{TagHelperOutputVariableName}.{ViewComponentsApi.TagHelperOutput.ContentPropertyName}",
                ViewComponentsApi.TagHelperOutput.ContentSetMethodName,
                arguments: [TagHelperContentVariableName]);
        }
    }

    private static void WriteProcessInvokeAsyncArgsMethodString(CodeWriter writer, TagHelperDescriptor tagHelper)
    {
        var methodReturnType = "Dictionary<string, object>";
        using (writer.BuildMethodDeclaration(
            CommonModifiers.Private,
            methodReturnType,
            ViewComponentsApi.ProcessInvokeAsyncArgsMethodName,
            [(ViewComponentsApi.TagHelperContext.FullTypeName, TagHelperContextVariableName)]))
        {
            writer.WriteStartAssignment($"{methodReturnType} args")
                .WriteStartNewObject(methodReturnType)
                .WriteEndMethodInvocation();

            foreach (var attribute in tagHelper.BoundAttributes)
            {
                var attributeName = attribute.Name;
                var parameterName = attribute.PropertyName;
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

    private static ImmutableArray<string> GetMethodArguments(TagHelperDescriptor tagHelper)
    {
        var viewComponentName = tagHelper.GetViewComponentName();

        return [$"\"{viewComponentName}\"", $"{ViewComponentsApi.ProcessInvokeAsyncArgsMethodName}({TagHelperContextVariableName})"];
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
