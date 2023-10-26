﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language.Components;

/// <summary>
/// Generates the C# code corresponding to Razor source document contents.
/// </summary>
internal class ComponentRuntimeNodeWriter : ComponentNodeWriter
{
    private readonly List<IntermediateToken> _currentAttributeValues = new List<IntermediateToken>();
    private readonly ScopeStack _scopeStack = new ScopeStack();
    private int _sourceSequence;

    public ComponentRuntimeNodeWriter(RazorLanguageVersion version) : base(version)
    {
    }

    public override void WriteCSharpCode(CodeRenderingContext context, CSharpCodeIntermediateNode node)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

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
            // The runtime and design time code differ in their handling of whitespace-only
            // statements. At runtime we can discard them completely. At design time we need
            // to keep them for the editor.
            return;
        }

        IDisposable linePragmaScope = null;
        if (node.Source != null)
        {
            linePragmaScope = context.CodeWriter.BuildLinePragma(node.Source.Value, context);
            context.CodeWriter.WritePadding(0, node.Source.Value, context);
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            if (node.Children[i] is IntermediateToken token && token.IsCSharp)
            {
                context.AddSourceMappingFor(token);
                context.CodeWriter.Write(token.Content);
            }
            else
            {
                // There may be something else inside the statement like an extension node.
                context.RenderNode(node.Children[i]);
            }
        }

        if (linePragmaScope != null)
        {
            linePragmaScope.Dispose();
        }
        else
        {
            context.CodeWriter.WriteLine();
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

        var sourceSequenceAsString = _sourceSequence.ToString(CultureInfo.InvariantCulture);
        var methodInvocation = _scopeStack.BuilderVarName + '.' + ComponentsApi.RenderTreeBuilder.AddContent + '(' + sourceSequenceAsString;
        _sourceSequence++;
        var parameterSeparatorLength = 2;

        using (context.CodeWriter.BuildEnhancedLinePragma(node.Source.Value, context, methodInvocation.Length + parameterSeparatorLength))
        {
            // Since we're not in the middle of writing an element, this must evaluate as some
            // text to display
            context.CodeWriter
                .Write(methodInvocation)
                .WriteParameterSeparator();

            for (var i = 0; i < node.Children.Count; i++)
            {
                if (node.Children[i] is IntermediateToken token && token.IsCSharp)
                {
                    WriteCSharpToken(context, token, includeLinePragma: false);
                }
                else
                {
                    // There may be something else inside the expression like a Template or another extension node.
                    context.RenderNode(node.Children[i]);
                }
            }
            context.CodeWriter.WriteEndMethodInvocation();
        }
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

        // In cases like "somestring @variable", Razor tokenizes it as:
        //  [0] HtmlContent="somestring"
        //  [1] CsharpContent="variable" Prefix=" "
        // ... so to avoid losing whitespace, convert the prefix to a further token in the list
        if (!string.IsNullOrEmpty(node.Prefix))
        {
            _currentAttributeValues.Add(new IntermediateToken() { Kind = TokenKind.Html, Content = node.Prefix });
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            _currentAttributeValues.Add((IntermediateToken)node.Children[i]);
        }
    }

    public override void WriteMarkupBlock(CodeRenderingContext context, MarkupBlockIntermediateNode node)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        context.CodeWriter
            .WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{ComponentsApi.RenderTreeBuilder.AddMarkupContent}")
            .Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture))
            .WriteParameterSeparator()
            .WriteStringLiteral(node.Content)
            .WriteEndMethodInvocation();
    }

    public override void WriteMarkupElement(CodeRenderingContext context, MarkupElementIntermediateNode node)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        context.CodeWriter
            .WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{ComponentsApi.RenderTreeBuilder.OpenElement}")
            .Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture))
            .WriteParameterSeparator()
            .WriteStringLiteral(node.TagName)
            .WriteEndMethodInvocation();

        bool hasFormName = false;

        // Render attributes and splats (in order) before creating the scope.
        foreach (var child in node.Children)
        {
            if (child is HtmlAttributeIntermediateNode attribute)
            {
                context.RenderNode(attribute);
            }
            else if (child is ComponentAttributeIntermediateNode componentAttribute)
            {
                context.RenderNode(componentAttribute);
            }
            else if (child is SplatIntermediateNode splat)
            {
                context.RenderNode(splat);
            }
            else if (child is FormNameIntermediateNode formName)
            {
                Debug.Assert(!hasFormName);
                context.RenderNode(formName);
                hasFormName = true;
            }
        }

        foreach (var setKey in node.SetKeys)
        {
            context.RenderNode(setKey);
        }

        foreach (var capture in node.Captures)
        {
            context.RenderNode(capture);
        }

        // AddNamedEvent must be called after all attributes (but before child content).
        if (hasFormName)
        {
            // _builder.AddNamedEvent("onsubmit", __formName);
            context.CodeWriter.Write(_scopeStack.BuilderVarName);
            context.CodeWriter.Write(".");
            context.CodeWriter.Write(ComponentsApi.RenderTreeBuilder.AddNamedEvent);
            context.CodeWriter.Write("(\"onsubmit\", ");
            context.CodeWriter.Write(_scopeStack.FormNameVarName);
            context.CodeWriter.Write(");");
            context.CodeWriter.WriteLine();
            _scopeStack.IncrementFormName();
        }

        // Render body of the tag inside the scope
        foreach (var child in node.Body)
        {
            context.RenderNode(child);
        }

        context.CodeWriter
            .WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{ComponentsApi.RenderTreeBuilder.CloseElement}")
            .WriteEndMethodInvocation();
    }

    public override void WriteHtmlAttribute(CodeRenderingContext context, HtmlAttributeIntermediateNode node)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        Debug.Assert(_currentAttributeValues.Count == 0);
        context.RenderChildren(node);

        if (node.AttributeNameExpression == null)
        {
            WriteAttribute(context, node.AttributeName, _currentAttributeValues);
        }
        else
        {
            WriteAttribute(context, node.AttributeNameExpression, _currentAttributeValues);
        }
        _currentAttributeValues.Clear();

        if (!string.IsNullOrEmpty(node.EventUpdatesAttributeName))
        {
            context.CodeWriter
                .WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{ComponentsApi.RenderTreeBuilder.SetUpdatesAttributeName}")
                .WriteStringLiteral(node.EventUpdatesAttributeName)
                .WriteEndMethodInvocation();
        }
    }

    public override void WriteHtmlAttributeValue(CodeRenderingContext context, HtmlAttributeValueIntermediateNode node)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        var stringContent = ((IntermediateToken)node.Children.Single()).Content;
        _currentAttributeValues.Add(new IntermediateToken() { Kind = TokenKind.Html, Content = node.Prefix + stringContent, });
    }

    public override void WriteHtmlContent(CodeRenderingContext context, HtmlContentIntermediateNode node)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        // Text node
        var content = GetHtmlContent(node);
        var renderApi = ComponentsApi.RenderTreeBuilder.AddContent;
        if (node.IsEncoded())
        {
            // This content is already encoded.
            renderApi = ComponentsApi.RenderTreeBuilder.AddMarkupContent;
        }

        context.CodeWriter
            .WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{renderApi}")
            .Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture))
            .WriteParameterSeparator()
            .WriteStringLiteral(content)
            .WriteEndMethodInvocation();
    }

    public override void WriteUsingDirective(CodeRenderingContext context, UsingDirectiveIntermediateNode node)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (node.Source.HasValue)
        {
            using (context.CodeWriter.BuildLinePragma(node.Source.Value, context))
            {
                context.CodeWriter.WriteUsing(node.Content);
            }
        }
        else
        {
            context.CodeWriter.WriteUsing(node.Content, endLine: true);
        }
    }

    public override void WriteComponent(CodeRenderingContext context, ComponentIntermediateNode node)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (ShouldSuppressTypeInferenceCall(node))
        {
        }
        else if (node.TypeInferenceNode == null)
        {
            // If the component is not using type inference then we just write an open/close with a series
            // of add attribute calls in between.
            //
            // Writes something like:
            //
            // _builder.OpenComponent<MyComponent>(0);
            // _builder.AddComponentParameter(1, "Foo", ...);
            // _builder.AddComponentParameter(2, "ChildContent", ...);
            // _builder.SetKey(someValue);
            // _builder.AddElementCapture(3, (__value) => _field = __value);
            // _builder.CloseComponent();

            // _builder.OpenComponent<TComponent>(42);
            context.CodeWriter.Write(_scopeStack.BuilderVarName);
            context.CodeWriter.Write(".");
            context.CodeWriter.Write(ComponentsApi.RenderTreeBuilder.OpenComponent);
            context.CodeWriter.Write("<");
            TypeNameHelper.WriteGloballyQualifiedName(context.CodeWriter, node.TypeName);
            context.CodeWriter.Write(">(");
            context.CodeWriter.Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture));
            context.CodeWriter.Write(");");
            context.CodeWriter.WriteLine();

            // We can skip type arguments during runtime codegen, they are handled in the
            // type/parameter declarations.

            bool hasRenderMode = false;

            // Preserve order of attributes and splats
            foreach (var child in node.Children)
            {
                if (child is ComponentAttributeIntermediateNode attribute)
                {
                    context.RenderNode(attribute);
                }
                else if (child is SplatIntermediateNode splat)
                {
                    context.RenderNode(splat);
                }
                else if (child is RenderModeIntermediateNode renderMode)
                {
                    Debug.Assert(!hasRenderMode);
                    context.RenderNode(renderMode);
                    hasRenderMode = true;
                }
            }

            foreach (var childContent in node.ChildContents)
            {
                context.RenderNode(childContent);
            }

            foreach (var setKey in node.SetKeys)
            {
                context.RenderNode(setKey);
            }

            foreach (var capture in node.Captures)
            {
                context.RenderNode(capture);
            }

            if (hasRenderMode)
            {
                // _builder.AddComponentRenderMode(__renderMode_0);
                WriteAddComponentRenderMode(context, _scopeStack.BuilderVarName, _scopeStack.RenderModeVarName);
                _scopeStack.IncrementRenderMode();
            }

            // _builder.CloseComponent();
            context.CodeWriter.Write(_scopeStack.BuilderVarName);
            context.CodeWriter.Write(".");
            context.CodeWriter.Write(ComponentsApi.RenderTreeBuilder.CloseComponent);
            context.CodeWriter.Write("();");
            context.CodeWriter.WriteLine();
        }
        else
        {
            var parameters = GetTypeInferenceMethodParameters(node.TypeInferenceNode);

            // If this component is going to cascade any of its generic types, we have to split its type inference
            // into two parts. First we call an inference method that captures all the parameters in local variables,
            // then we use those to call the real type inference method that emits the component. The reason for this
            // is so the captured variables can be used by descendants without re-evaluating the expressions.
            CodeWriterExtensions.CSharpCodeWritingScope? typeInferenceCaptureScope = null;
            if (node.Component.SuppliesCascadingGenericParameters())
            {
                typeInferenceCaptureScope = context.CodeWriter.BuildScope();
                TypeNameHelper.WriteGloballyQualifiedName(context.CodeWriter, node.TypeInferenceNode.FullTypeName);
                context.CodeWriter.Write(".");
                context.CodeWriter.Write(node.TypeInferenceNode.MethodName);
                context.CodeWriter.Write("_CaptureParameters(");
                var isFirst = true;
                foreach (var parameter in parameters.Where(p => p.UsedForTypeInference))
                {
                    if (isFirst)
                    {
                        isFirst = false;
                    }
                    else
                    {
                        context.CodeWriter.Write(", ");
                    }

                    WriteTypeInferenceMethodParameterInnards(context, parameter);
                    context.CodeWriter.Write(", out var ");

                    var variableName = $"__typeInferenceArg_{_scopeStack.Depth}_{parameter.ParameterName}";
                    context.CodeWriter.Write(variableName);

                    UseCapturedCascadingGenericParameterVariable(node, parameter, variableName);
                }
                context.CodeWriter.WriteLine(");");
            }

            // When we're doing type inference, we can't write all of the code inline to initialize
            // the component on the builder. We generate a method elsewhere, and then pass all of the information
            // to that method. We pass in all of the attribute values + the sequence numbers.
            //
            // __Blazor.MyComponent.TypeInference.CreateMyComponent_0(builder, 0, 1, ..., 2, ..., 3, ...);

            TypeNameHelper.WriteGloballyQualifiedName(context.CodeWriter, node.TypeInferenceNode.FullTypeName);
            context.CodeWriter.Write(".");
            context.CodeWriter.Write(node.TypeInferenceNode.MethodName);
            context.CodeWriter.Write("(");

            context.CodeWriter.Write(_scopeStack.BuilderVarName);
            context.CodeWriter.Write(", ");

            context.CodeWriter.Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture));

            foreach (var parameter in parameters)
            {
                context.CodeWriter.Write(", ");

                if (!string.IsNullOrEmpty(parameter.SeqName))
                {
                    context.CodeWriter.Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture));
                    context.CodeWriter.Write(", ");
                }

                WriteTypeInferenceMethodParameterInnards(context, parameter);
            }

            context.CodeWriter.Write(");");
            context.CodeWriter.WriteLine();

            if (typeInferenceCaptureScope.HasValue)
            {
                foreach (var localToClear in parameters.Select(p => p.Source).OfType<TypeInferenceCapturedVariable>())
                {
                    // Ensure we're not interfering with the GC lifetime of these captured values
                    // We don't need the values any longer (code in closures only uses its types for compile-time inference)
                    context.CodeWriter.Write(localToClear.VariableName);
                    context.CodeWriter.WriteLine(" = default;");
                }
                typeInferenceCaptureScope.Value.Dispose();
            }
        }
    }

    private void WriteTypeInferenceMethodParameterInnards(CodeRenderingContext context, TypeInferenceMethodParameter parameter)
    {
        switch (parameter.Source)
        {
            case ComponentAttributeIntermediateNode attribute:
                // Don't type check generics, since we can't actually write the type name.
                // The type checking will happen anyway since we defined a method and we're generating
                // a call to it.
                WriteComponentAttributeInnards(context, attribute, canTypeCheck: false);
                break;
            case SplatIntermediateNode splat:
                WriteSplatInnards(context, splat, canTypeCheck: false);
                break;
            case ComponentChildContentIntermediateNode childNode:
                WriteComponentChildContentInnards(context, childNode);
                break;
            case SetKeyIntermediateNode setKey:
                WriteSetKeyInnards(context, setKey);
                break;
            case ReferenceCaptureIntermediateNode capture:
                WriteReferenceCaptureInnards(context, capture, shouldTypeCheck: false);
                break;
            case CascadingGenericTypeParameter syntheticArg:
                // The value should be populated before we use it, because we emit code for creating ancestors
                // first, and that's where it's populated. However if this goes wrong somehow, we don't want to
                // throw, so use a fallback
                var valueExpression = syntheticArg.ValueExpression ?? "default";
                context.CodeWriter.Write(valueExpression);
                if (!context.Options.SuppressNullabilityEnforcement && IsDefaultExpression(valueExpression))
                {
                    context.CodeWriter.Write("!");
                }
                break;
            case TypeInferenceCapturedVariable capturedVariable:
                context.CodeWriter.Write(capturedVariable.VariableName);
                break;
            case RenderModeIntermediateNode renderMode:
                WriteCSharpCode(context, new CSharpCodeIntermediateNode() { Source = renderMode.Source, Children = { renderMode.Children[0] } });
                break;
            default:
                throw new InvalidOperationException($"Not implemented: type inference method parameter from source {parameter.Source}");
        }
    }

    public override void WriteComponentAttribute(CodeRenderingContext context, ComponentAttributeIntermediateNode node)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (node.IsDesignTimePropertyAccessHelper())
        {
            return;
        }

        var addAttributeMethod = node.Annotations[ComponentMetadata.Common.AddAttributeMethodName] as string ?? GetAddComponentParameterMethodName(context);

        // _builder.AddComponentParameter(1, "Foo", 42);
        context.CodeWriter.Write(_scopeStack.BuilderVarName);
        context.CodeWriter.Write(".");
        context.CodeWriter.Write(addAttributeMethod);
        context.CodeWriter.Write("(");
        context.CodeWriter.Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture));
        context.CodeWriter.Write(", ");
        context.CodeWriter.WriteStringLiteral(node.AttributeName);
        context.CodeWriter.Write(", ");

        if (addAttributeMethod == ComponentsApi.RenderTreeBuilder.AddAttribute)
        {
            context.CodeWriter.Write("(object)(");
        }

        WriteComponentAttributeInnards(context, node, canTypeCheck: true);

        if (addAttributeMethod == ComponentsApi.RenderTreeBuilder.AddAttribute)
        {
            context.CodeWriter.Write(")");
        }

        context.CodeWriter.Write(");");
        context.CodeWriter.WriteLine();
    }

    private void WriteComponentAttributeInnards(CodeRenderingContext context, ComponentAttributeIntermediateNode node, bool canTypeCheck)
    {
        if (node.AttributeStructure == AttributeStructure.Minimized)
        {
            // Minimized attributes always map to 'true'
            context.CodeWriter.Write("true");
        }
        else if (node.Children.Count > 1)
        {
            // We don't expect this to happen, we just want to know if it can.
            throw new InvalidOperationException("Attribute nodes should either be minimized or a single type of content." + string.Join(", ", node.Children));
        }
        else if (node.Children.Count == 1 && node.Children[0] is HtmlContentIntermediateNode htmlNode)
        {
            // This is how string attributes are lowered by default, a single HTML node with a single HTML token.
            var content = string.Join(string.Empty, GetHtmlTokens(htmlNode).Select(t => t.Content));
            context.CodeWriter.WriteStringLiteral(content);
        }
        else
        {
            // See comments in ComponentDesignTimeNodeWriter for a description of the cases that are possible.
            var tokens = GetCSharpTokens(node);
            if ((node.BoundAttribute?.IsDelegateProperty() ?? false) ||
                (node.BoundAttribute?.IsChildContentProperty() ?? false))
            {
                if (canTypeCheck)
                {
                    context.CodeWriter.Write("(");
                    TypeNameHelper.WriteGloballyQualifiedName(context.CodeWriter, node.TypeName);
                    context.CodeWriter.Write(")");
                    context.CodeWriter.Write("(");
                }

                for (var i = 0; i < tokens.Count; i++)
                {
                    WriteCSharpToken(context, tokens[i]);
                }

                if (canTypeCheck)
                {
                    context.CodeWriter.Write(")");
                }
            }
            else if (node.BoundAttribute?.IsEventCallbackProperty() ?? false)
            {
                var explicitType = (bool?)node.Annotations[ComponentMetadata.Component.ExplicitTypeNameKey];
                var isInferred = (bool?)node.Annotations[ComponentMetadata.Component.OpenGenericKey];
                if (canTypeCheck && NeedsTypeCheck(node))
                {
                    context.CodeWriter.Write(ComponentsApi.RuntimeHelpers.TypeCheck);
                    context.CodeWriter.Write("<");
                    QualifyEventCallback(context.CodeWriter, node.TypeName, explicitType);
                    context.CodeWriter.Write(">");
                    context.CodeWriter.Write("(");
                }

                // Microsoft.AspNetCore.Components.EventCallback.Factory.Create(this, ...) OR
                // Microsoft.AspNetCore.Components.EventCallback.Factory.Create<T>(this, ...)

                context.CodeWriter.Write("global::");
                context.CodeWriter.Write(ComponentsApi.EventCallback.FactoryAccessor);
                context.CodeWriter.Write(".");
                context.CodeWriter.Write(ComponentsApi.EventCallbackFactory.CreateMethod);

                if (isInferred != true && node.TryParseEventCallbackTypeArgument(out ReadOnlyMemory<char> argument))
                {
                    context.CodeWriter.Write("<");
                    if (explicitType == true)
                    {
                        context.CodeWriter.Write(argument);
                    }
                    else
                    {
                        TypeNameHelper.WriteGloballyQualifiedName(context.CodeWriter, argument);
                    }
                    context.CodeWriter.Write(">");
                }

                context.CodeWriter.Write("(");
                context.CodeWriter.Write("this");
                context.CodeWriter.Write(", ");

                for (var i = 0; i < tokens.Count; i++)
                {
                    WriteCSharpToken(context, tokens[i]);
                }

                context.CodeWriter.Write(")");

                if (canTypeCheck && NeedsTypeCheck(node))
                {
                    context.CodeWriter.Write(")");
                }
            }
            else
            {
                if (canTypeCheck && NeedsTypeCheck(node))
                {
                    context.CodeWriter.Write(ComponentsApi.RuntimeHelpers.TypeCheck);
                    context.CodeWriter.Write("<");
                    var explicitType = (bool?)node.Annotations[ComponentMetadata.Component.ExplicitTypeNameKey];
                    if (explicitType == true)
                    {
                        context.CodeWriter.Write(node.TypeName);
                    }
                    else
                    {
                        TypeNameHelper.WriteGloballyQualifiedName(context.CodeWriter, node.TypeName);
                    }
                    context.CodeWriter.Write(">");
                    context.CodeWriter.Write("(");
                }

                for (var i = 0; i < tokens.Count; i++)
                {
                    WriteCSharpToken(context, tokens[i]);
                }

                if (canTypeCheck && NeedsTypeCheck(node))
                {
                    context.CodeWriter.Write(")");
                }

            }

            static void QualifyEventCallback(CodeWriter codeWriter, string typeName, bool? explicitType)
            {
                if (ComponentAttributeIntermediateNode.TryGetEventCallbackArgument(typeName.AsMemory(), out var argument))
                {
                    codeWriter.Write("global::");
                    codeWriter.Write(ComponentsApi.EventCallback.FullTypeName);
                    codeWriter.Write("<");
                    if (explicitType == true)
                    {
                        codeWriter.Write(argument);
                    }
                    else
                    {
                        TypeNameHelper.WriteGloballyQualifiedName(codeWriter, argument);
                    }
                    codeWriter.Write(">");
                }
                else
                {
                    TypeNameHelper.WriteGloballyQualifiedName(codeWriter, typeName);
                }
            }
        }

        IReadOnlyList<IntermediateToken> GetHtmlTokens(HtmlContentIntermediateNode html)
        {
            // We generally expect all children to be HTML, this is here just in case.
            return html.FindDescendantNodes<IntermediateToken>().Where(t => t.IsHtml).ToArray();
        }

        static bool NeedsTypeCheck(ComponentAttributeIntermediateNode n)
        {
            return n.BoundAttribute != null && !n.BoundAttribute.IsWeaklyTyped();
        }
    }

    private IReadOnlyList<IntermediateToken> GetCSharpTokens(IntermediateNode node)
    {
        // We generally expect all children to be CSharp, this is here just in case.
        return node.FindDescendantNodes<IntermediateToken>().Where(t => t.IsCSharp).ToArray();
    }

    public override void WriteComponentChildContent(CodeRenderingContext context, ComponentChildContentIntermediateNode node)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        // Writes something like:
        //
        // _builder.AddComponentParameter(1, "ChildContent", (RenderFragment)((__builder73) => { ... }));
        // OR
        // _builder.AddComponentParameter(1, "ChildContent", (RenderFragment<Person>)((person) => (__builder73) => { ... }));
        BeginWriteAttribute(context, node.AttributeName);
        context.CodeWriter.WriteParameterSeparator();
        context.CodeWriter.Write("(");
        TypeNameHelper.WriteGloballyQualifiedName(context.CodeWriter, node.TypeName);
        context.CodeWriter.Write(")(");

        WriteComponentChildContentInnards(context, node);

        context.CodeWriter.Write(")");
        context.CodeWriter.WriteEndMethodInvocation();
    }

    private void WriteComponentChildContentInnards(CodeRenderingContext context, ComponentChildContentIntermediateNode node)
    {
        // Writes something like:
        //
        // ((__builder73) => { ... })
        // OR
        // ((person) => (__builder73) => { })
        _scopeStack.OpenComponentScope(
            context,
            node.AttributeName,
            node.IsParameterized ? node.ParameterName : null);
        for (var i = 0; i < node.Children.Count; i++)
        {
            context.RenderNode(node.Children[i]);
        }
        _scopeStack.CloseScope(context);
    }

    public override void WriteComponentTypeArgument(CodeRenderingContext context, ComponentTypeArgumentIntermediateNode node)
    {
        // We can skip type arguments during runtime codegen, they are handled in the
        // type/parameter declarations.
    }

    public override void WriteTemplate(CodeRenderingContext context, TemplateIntermediateNode node)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        // Looks like:
        //
        // (__builder73) => { ... }
        _scopeStack.OpenTemplateScope(context);
        context.RenderChildren(node);
        _scopeStack.CloseScope(context);
    }

    public override void WriteSetKey(CodeRenderingContext context, SetKeyIntermediateNode node)
    {
        // Looks like:
        //
        // _builder.SetKey(_keyValue);

        var codeWriter = context.CodeWriter;

        codeWriter
            .WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{ComponentsApi.RenderTreeBuilder.SetKey}");
        WriteSetKeyInnards(context, node);
        codeWriter.WriteEndMethodInvocation();
    }

    private void WriteSetKeyInnards(CodeRenderingContext context, SetKeyIntermediateNode node)
    {
        WriteCSharpCode(context, new CSharpCodeIntermediateNode
        {
            Source = node.Source,
            Children =
                    {
                        node.KeyValueToken
                    }
        });
    }

    public override void WriteSplat(CodeRenderingContext context, SplatIntermediateNode node)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        // Looks like:
        //
        // _builder.AddMultipleAttributes(2, ...);
        context.CodeWriter.WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{ComponentsApi.RenderTreeBuilder.AddMultipleAttributes}");
        context.CodeWriter.Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture));
        context.CodeWriter.WriteParameterSeparator();

        WriteSplatInnards(context, node, canTypeCheck: true);

        context.CodeWriter.WriteEndMethodInvocation();
    }

    private void WriteSplatInnards(CodeRenderingContext context, SplatIntermediateNode node, bool canTypeCheck)
    {
        var tokens = GetCSharpTokens(node);

        if (canTypeCheck)
        {
            context.CodeWriter.Write(ComponentsApi.RuntimeHelpers.TypeCheck);
            context.CodeWriter.Write("<");
            context.CodeWriter.Write(ComponentsApi.AddMultipleAttributesTypeFullName);
            context.CodeWriter.Write(">");
            context.CodeWriter.Write("(");
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            WriteCSharpToken(context, tokens[i]);
        }

        if (canTypeCheck)
        {
            context.CodeWriter.Write(")");
        }
    }

    public sealed override void WriteFormName(CodeRenderingContext context, FormNameIntermediateNode node)
    {
        // string __formName = expression;
        context.CodeWriter.Write("string ");
        context.CodeWriter.Write(_scopeStack.FormNameVarName);
        context.CodeWriter.Write(" = ");
        context.CodeWriter.Write(ComponentsApi.RuntimeHelpers.TypeCheck);
        context.CodeWriter.Write("<string>(");
        WriteAttributeValue(context, node.FindDescendantNodes<IntermediateToken>());
        context.CodeWriter.Write(")");
        context.CodeWriter.WriteLine(";");
    }

    public override void WriteReferenceCapture(CodeRenderingContext context, ReferenceCaptureIntermediateNode node)
    {
        // Looks like:
        //
        // _builder.AddComponentReferenceCapture(2, (__value) = { _field = (MyComponent)__value; });
        // OR
        // _builder.AddElementReferenceCapture(2, (__value) = { _field = (ElementReference)__value; });
        var codeWriter = context.CodeWriter;

        var methodName = node.IsComponentCapture
            ? ComponentsApi.RenderTreeBuilder.AddComponentReferenceCapture
            : ComponentsApi.RenderTreeBuilder.AddElementReferenceCapture;
        codeWriter
            .WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{methodName}")
            .Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture))
            .WriteParameterSeparator();

        WriteReferenceCaptureInnards(context, node, shouldTypeCheck: true);

        codeWriter.WriteEndMethodInvocation();
    }

    protected override void WriteReferenceCaptureInnards(CodeRenderingContext context, ReferenceCaptureIntermediateNode node, bool shouldTypeCheck)
    {
        // Looks like:
        //
        // (__value) = { _field = (MyComponent)__value; }
        // OR
        // (__value) = { _field = (ElementRef)__value; }
        const string refCaptureParamName = "__value";
        using (var lambdaScope = context.CodeWriter.BuildLambda(refCaptureParamName))
        {
            var typecastIfNeeded = shouldTypeCheck && node.IsComponentCapture ? $"({node.ComponentCaptureTypeName})" : string.Empty;
            WriteCSharpCode(context, new CSharpCodeIntermediateNode
            {
                Source = node.Source,
                Children =
                    {
                        node.IdentifierToken,
                        new IntermediateToken
                        {
                            Kind = TokenKind.CSharp,
                            Content = $" = {typecastIfNeeded}{refCaptureParamName};"
                        }
                    }
            });
        }
    }

    public override void WriteRenderMode(CodeRenderingContext context, RenderModeIntermediateNode node)
    {
        // Looks like:
        // global::Microsoft.AspNetCore.Components.IComponentRenderMode __renderMode0 = expression;
        WriteCSharpCode(context, new CSharpCodeIntermediateNode
        {
            Children =
            {
                new IntermediateToken
                {
                    Kind = TokenKind.CSharp,
                    Content = $"global::{ComponentsApi.IComponentRenderMode.FullTypeName} {_scopeStack.RenderModeVarName} = "
                },
                new CSharpCodeIntermediateNode
                {
                    Source = node.Source,
                    Children = { node.Children[0] }
                },
                new IntermediateToken
                {
                    Kind = TokenKind.CSharp,
                    Content = ";"
                }
            }
        });
    }

    private void WriteAttribute(CodeRenderingContext context, string key, IReadOnlyList<IntermediateToken> value)
    {
        BeginWriteAttribute(context, key);

        if (value.Count > 0)
        {
            context.CodeWriter.WriteParameterSeparator();
            WriteAttributeValue(context, value);
        }
        else if (!context.Options.OmitMinimizedComponentAttributeValues)
        {
            // In version 5+, there's no need to supply a value for a minimized attribute.
            // But for older language versions, minimized attributes were represented as "true".
            context.CodeWriter.WriteParameterSeparator();
            context.CodeWriter.WriteBooleanLiteral(true);
        }

        context.CodeWriter.WriteEndMethodInvocation();
    }

    private void WriteAttribute(CodeRenderingContext context, IntermediateNode nameExpression, IReadOnlyList<IntermediateToken> value)
    {
        BeginWriteAttribute(context, nameExpression);
        if (value.Count > 0)
        {
            context.CodeWriter.WriteParameterSeparator();
            WriteAttributeValue(context, value);
        }
        context.CodeWriter.WriteEndMethodInvocation();
    }

    protected override void BeginWriteAttribute(CodeRenderingContext context, string key)
    {
        context.CodeWriter
            .WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{ComponentsApi.RenderTreeBuilder.AddAttribute}")
            .Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture))
            .WriteParameterSeparator()
            .WriteStringLiteral(key);
    }

    protected override void BeginWriteAttribute(CodeRenderingContext context, IntermediateNode nameExpression)
    {
        context.CodeWriter.WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{ComponentsApi.RenderTreeBuilder.AddAttribute}");
        context.CodeWriter.Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture));
        context.CodeWriter.WriteParameterSeparator();

        var tokens = GetCSharpTokens(nameExpression);
        for (var i = 0; i < tokens.Count; i++)
        {
            WriteCSharpToken(context, tokens[i]);
        }
    }

    private static string GetHtmlContent(HtmlContentIntermediateNode node)
    {
        using var _ = StringBuilderPool.GetPooledObject(out var builder);

        var htmlTokens = node.Children.OfType<IntermediateToken>().Where(t => t.IsHtml);
        foreach (var htmlToken in htmlTokens)
        {
            builder.Append(htmlToken.Content);
        }

        return builder.ToString();
    }

    // There are a few cases here, we need to handle:
    // - Pure HTML
    // - Pure CSharp
    // - Mixed HTML and CSharp
    //
    // Only the mixed case is complicated, we want to turn it into code that will concatenate
    // the values into a string at runtime.

    private static void WriteAttributeValue(CodeRenderingContext context, IReadOnlyList<IntermediateToken> tokens)
    {
        if (tokens == null)
        {
            throw new ArgumentNullException(nameof(tokens));
        }

        var writer = context.CodeWriter;
        var hasHtml = false;
        var hasCSharp = false;
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].IsCSharp)
            {
                hasCSharp |= true;
            }
            else
            {
                hasHtml |= true;
            }
        }

        if (hasHtml && hasCSharp)
        {
            // If it's a C# expression, we have to wrap it in parens, otherwise things like ternary
            // expressions don't compose with concatenation. However, this is a little complicated
            // because C# tokens themselves aren't guaranteed to be distinct expressions. We want
            // to treat all contiguous C# tokens as a single expression.
            var insideCSharp = false;
            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (token.IsCSharp)
                {
                    if (!insideCSharp)
                    {
                        if (i != 0)
                        {
                            writer.Write(" + ");
                        }

                        writer.Write("(");
                        insideCSharp = true;
                    }

                    WriteCSharpToken(context, token);
                }
                else
                {
                    if (insideCSharp)
                    {
                        writer.Write(")");
                        insideCSharp = false;
                    }

                    if (i != 0)
                    {
                        writer.Write(" + ");
                    }

                    writer.WriteStringLiteral(token.Content);
                }
            }

            if (insideCSharp)
            {
                writer.Write(")");
            }
        }
        else if (hasCSharp)
        {
            foreach (var token in tokens)
            {
                WriteCSharpToken(context, token);
            }
        }
        else if (hasHtml)
        {
            writer.WriteStringLiteral(string.Join("", tokens.Select(t => t.Content)));
        }
        else
        {
            throw new InvalidOperationException("Found attribute whose value is neither HTML nor CSharp");
        }
    }

    private static void WriteCSharpToken(CodeRenderingContext context, IntermediateToken token, bool includeLinePragma = true)
    {
        if (string.IsNullOrWhiteSpace(token.Content))
        {
            return;
        }

        if (token.Source?.FilePath == null)
        {
            context.CodeWriter.Write(token.Content);
            return;
        }

        if (includeLinePragma)
        {
            using (context.CodeWriter.BuildLinePragma(token.Source, context))
            {
                context.CodeWriter.WritePadding(0, token.Source.Value, context);
                context.CodeWriter.Write(token.Content);
            }
            return;
        }

        context.CodeWriter.Write(token.Content);
    }
}
