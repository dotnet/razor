// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Components;

/// <summary>
/// Generates the C# code corresponding to Razor source document contents.
/// </summary>
internal class ComponentRuntimeNodeWriter(CodeRenderingContext context, RazorLanguageVersion version)
    : ComponentNodeWriter(context, version)
{
    private readonly ImmutableArray<IntermediateToken>.Builder _currentAttributeValues = ImmutableArray.CreateBuilder<IntermediateToken>();
    private readonly ScopeStack _scopeStack = new ScopeStack();
    private int _sourceSequence;

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

        if (node.Source is null && isWhitespaceStatement)
        {
            // If source is null, we won't create source mappings, and if we're not creating source mappings,
            // there is no point emitting whitespace
            return;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            if (node.Children[i] is CSharpIntermediateToken token)
            {
                WriteCSharpToken(token);
            }
            else
            {
                // There may be something else inside the statement like an extension node.
                Context.RenderNode(node.Children[i]);
            }
        }

        Context.CodeWriter.WriteLine();
    }

    public override void WriteCSharpExpression(CSharpExpressionIntermediateNode node)
    {
        var sourceSequenceAsString = _sourceSequence.ToString(CultureInfo.InvariantCulture);
        var methodInvocation = _scopeStack.BuilderVarName + '.' + ComponentsApi.RenderTreeBuilder.AddContent + '(' + sourceSequenceAsString;
        _sourceSequence++;

        // Sequence points can only be emitted when the eval stack is empty. That means we can't arbitrarily map everything that could be in
        // the node. Instead we map just the first C# child node by putting the pragma before we start the method invocation and offset it.
        // This is not a perfect mapping, but generally this works for most cases:
        // - Common case: there is only a single node and it is C#, so it maps correctly
        // - There is some C# followed by a render template: the C# gets mapped, and the render template issues a lambda call which conceptually
        //   is another method so a sequence point can be emitted. Unfortunately any trailing C# is not mapped, although in many cases it's uninteresting
        //   such as closing parenthesis.
        // - Error cases: there are no nodes, so we do nothing
        var firstCSharpChild = node.Children.OfType<CSharpIntermediateToken>().FirstOrDefault();
        using (Context.BuildEnhancedLinePragma(firstCSharpChild?.Source, characterOffset: methodInvocation.Length + 2))
        {
            Context.CodeWriter
                .Write(methodInvocation)
                .WriteParameterSeparator();
        
            if (firstCSharpChild is not null)
            {
                Context.CodeWriter.Write(firstCSharpChild.Content);
            }
        }

        // render the remaining children. We still emit the #line pragmas for the remaining csharp tokens but
        // these wont actually generate any sequence points for debugging.
        foreach (var child in node.Children)
        {
            if (child == firstCSharpChild)
            {
                continue;
            }
            else if (child is CSharpIntermediateToken csharpToken)
            {
                WriteCSharpToken(csharpToken);
            }
            else
            {
                // There may be something else inside the expression like a Template or another extension node.
                Context.RenderNode(child);
            }
        }

        Context.CodeWriter.WriteEndMethodInvocation();
    }

    public override void WriteCSharpExpressionAttributeValue(CSharpExpressionAttributeValueIntermediateNode node)
    {
        // In cases like "somestring @variable", Razor tokenizes it as:
        //  [0] HtmlContent="somestring"
        //  [1] CsharpContent="variable" Prefix=" "
        // ... so to avoid losing whitespace, convert the prefix to a further token in the list
        if (!string.IsNullOrEmpty(node.Prefix))
        {
            _currentAttributeValues.Add(NodeFactory.HtmlToken(node.Prefix));
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            _currentAttributeValues.Add((IntermediateToken)node.Children[i]);
        }
    }

    public override void WriteMarkupBlock(MarkupBlockIntermediateNode node)
    {
        Context.CodeWriter
            .WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{ComponentsApi.RenderTreeBuilder.AddMarkupContent}")
            .Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture))
            .WriteParameterSeparator()
            .WriteStringLiteral(node.Content)
            .WriteEndMethodInvocation();
    }

    public override void WriteMarkupElement(MarkupElementIntermediateNode node)
    {
        Context.CodeWriter
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
                Context.RenderNode(attribute);
            }
            else if (child is ComponentAttributeIntermediateNode componentAttribute)
            {
                Context.RenderNode(componentAttribute);
            }
            else if (child is SplatIntermediateNode splat)
            {
                Context.RenderNode(splat);
            }
            else if (child is FormNameIntermediateNode formName)
            {
                Debug.Assert(!hasFormName);
                Context.RenderNode(formName);
                hasFormName = true;
            }
        }

        foreach (var setKey in node.SetKeys)
        {
            Context.RenderNode(setKey);
        }

        foreach (var capture in node.Captures)
        {
            Context.RenderNode(capture);
        }

        // AddNamedEvent must be called after all attributes (but before child content).
        if (hasFormName)
        {
            // _builder.AddNamedEvent("onsubmit", __formName);
            Context.CodeWriter.Write(_scopeStack.BuilderVarName);
            Context.CodeWriter.Write(".");
            Context.CodeWriter.Write(ComponentsApi.RenderTreeBuilder.AddNamedEvent);
            Context.CodeWriter.Write("(\"onsubmit\", ");
            Context.CodeWriter.Write(_scopeStack.FormNameVarName);
            Context.CodeWriter.Write(");");
            Context.CodeWriter.WriteLine();
            _scopeStack.IncrementFormName();
        }

        // Render body of the tag inside the scope
        foreach (var child in node.Body)
        {
            Context.RenderNode(child);
        }

        Context.CodeWriter
            .WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{ComponentsApi.RenderTreeBuilder.CloseElement}")
            .WriteEndMethodInvocation();
    }

    public override void WriteHtmlAttribute(HtmlAttributeIntermediateNode node)
    {
        Debug.Assert(_currentAttributeValues.Count == 0);
        Context.RenderChildren(node);

        if (node.AttributeNameExpression == null)
        {
            WriteAttribute(node.AttributeName, _currentAttributeValues.ToImmutableAndClear());
        }
        else
        {
            WriteAttribute(node.AttributeNameExpression, _currentAttributeValues.ToImmutableAndClear());
        }

        if (!string.IsNullOrEmpty(node.EventUpdatesAttributeName))
        {
            Context.CodeWriter
                .WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{ComponentsApi.RenderTreeBuilder.SetUpdatesAttributeName}")
                .WriteStringLiteral(node.EventUpdatesAttributeName)
                .WriteEndMethodInvocation();
        }
    }

    public override void WriteHtmlAttributeValue(HtmlAttributeValueIntermediateNode node)
    {
        var stringContent = ((IntermediateToken)node.Children.Single()).Content;
        _currentAttributeValues.Add(NodeFactory.HtmlToken($"{node.Prefix}{stringContent}"));
    }

    public override void WriteHtmlContent(HtmlContentIntermediateNode node)
    {
        // Text node
        var content = node.GetContent();
        var renderApi = ComponentsApi.RenderTreeBuilder.AddContent;
        if (node.HasEncodedContent)
        {
            // This content is already encoded.
            renderApi = ComponentsApi.RenderTreeBuilder.AddMarkupContent;
        }

        Context.CodeWriter
            .WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{renderApi}")
            .Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture))
            .WriteParameterSeparator()
            .WriteStringLiteral(content)
            .WriteEndMethodInvocation();
    }

    public override void WriteUsingDirective(UsingDirectiveIntermediateNode node)
    {
        if (node.Source is { FilePath: not null } sourceSpan)
        {
            using (Context.BuildEnhancedLinePragma(sourceSpan, suppressLineDefaultAndHidden: true))
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
            Context.CodeWriter.WriteUsing(node.Content, endLine: true);

            if (node.AppendLineDefaultAndHidden)
            {
                Context.CodeWriter.WriteLine("#line default");
                Context.CodeWriter.WriteLine("#line hidden");
            }
        }
    }

    public override void WriteComponent(ComponentIntermediateNode node)
    {
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
            Context.CodeWriter.Write(_scopeStack.BuilderVarName);
            Context.CodeWriter.Write(".");
            Context.CodeWriter.Write(ComponentsApi.RenderTreeBuilder.OpenComponent);
            Context.CodeWriter.Write("<");

            TypeNameHelper.WriteGloballyQualifiedName(Context.CodeWriter, TypeNameHelper.GetNonGenericTypeName(node.TypeName));
            if (!node.OrderedTypeArguments.IsDefaultOrEmpty)
            {
                Context.CodeWriter.Write("<");
                for (var i = 0; i < node.OrderedTypeArguments.Length; i++)
                {
                    var typeArg = node.OrderedTypeArguments[i];
                    WriteComponentTypeArgument(typeArg);
                    if (i != node.OrderedTypeArguments.Length - 1)
                    {
                        Context.CodeWriter.Write(", ");
                    }
                }

                Context.CodeWriter.Write(">");
            }

            Context.CodeWriter.Write(">(");
            Context.CodeWriter.Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture));
            Context.CodeWriter.Write(");");
            Context.CodeWriter.WriteLine();

            // We can skip type arguments during runtime codegen, they are handled in the
            // type/parameter declarations.

            bool hasRenderMode = false;

            // Preserve order of attributes and splats
            foreach (var child in node.Children)
            {
                if (child is ComponentAttributeIntermediateNode attribute)
                {
                    Context.RenderNode(attribute);
                }
                else if (child is SplatIntermediateNode splat)
                {
                    Context.RenderNode(splat);
                }
                else if (child is RenderModeIntermediateNode renderMode)
                {
                    Debug.Assert(!hasRenderMode);
                    Context.RenderNode(renderMode);
                    hasRenderMode = true;
                }
            }

            foreach (var childContent in node.ChildContents)
            {
                Context.RenderNode(childContent);
            }

            foreach (var setKey in node.SetKeys)
            {
                Context.RenderNode(setKey);
            }

            foreach (var capture in node.Captures)
            {
                Context.RenderNode(capture);
            }

            if (hasRenderMode)
            {
                // _builder.AddComponentRenderMode(__renderMode_0);
                WriteAddComponentRenderMode(_scopeStack.BuilderVarName, _scopeStack.RenderModeVarName);
                _scopeStack.IncrementRenderMode();
            }

            // _builder.CloseComponent();
            Context.CodeWriter.Write(_scopeStack.BuilderVarName);
            Context.CodeWriter.Write(".");
            Context.CodeWriter.Write(ComponentsApi.RenderTreeBuilder.CloseComponent);
            Context.CodeWriter.Write("();");
            Context.CodeWriter.WriteLine();
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
                typeInferenceCaptureScope = Context.CodeWriter.BuildScope();
                TypeNameHelper.WriteGloballyQualifiedName(Context.CodeWriter, node.TypeInferenceNode.FullTypeName);
                Context.CodeWriter.Write(".");
                Context.CodeWriter.Write(node.TypeInferenceNode.MethodName);
                Context.CodeWriter.Write("_CaptureParameters(");
                var isFirst = true;
                foreach (var parameter in parameters.Where(p => p.UsedForTypeInference))
                {
                    if (isFirst)
                    {
                        isFirst = false;
                    }
                    else
                    {
                        Context.CodeWriter.Write(", ");
                    }

                    WriteTypeInferenceMethodParameterInnards(parameter);
                    Context.CodeWriter.Write(", out var ");

                    var variableName = $"__typeInferenceArg_{_scopeStack.Depth}_{parameter.ParameterName}";
                    Context.CodeWriter.Write(variableName);

                    UseCapturedCascadingGenericParameterVariable(node, parameter, variableName);
                }

                Context.CodeWriter.WriteLine(");");
            }

            // When we're doing type inference, we can't write all of the code inline to initialize
            // the component on the builder. We generate a method elsewhere, and then pass all of the information
            // to that method. We pass in all of the attribute values + the sequence numbers.
            //
            // __Blazor.MyComponent.TypeInference.CreateMyComponent_0(builder, 0, 1, ..., 2, ..., 3, ...);

            TypeNameHelper.WriteGloballyQualifiedName(Context.CodeWriter, node.TypeInferenceNode.FullTypeName);
            Context.CodeWriter.Write(".");
            Context.CodeWriter.Write(node.TypeInferenceNode.MethodName);
            Context.CodeWriter.Write("(");

            Context.CodeWriter.Write(_scopeStack.BuilderVarName);
            Context.CodeWriter.Write(", ");

            Context.CodeWriter.Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture));

            foreach (var parameter in parameters)
            {
                Context.CodeWriter.Write(", ");

                if (!string.IsNullOrEmpty(parameter.SeqName))
                {
                    Context.CodeWriter.Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture));
                    Context.CodeWriter.Write(", ");
                }

                WriteTypeInferenceMethodParameterInnards(parameter);
            }

            Context.CodeWriter.Write(");");
            Context.CodeWriter.WriteLine();

            if (typeInferenceCaptureScope.HasValue)
            {
                foreach (var localToClear in parameters.Select(p => p.Source).OfType<TypeInferenceCapturedVariable>())
                {
                    // Ensure we're not interfering with the GC lifetime of these captured values
                    // We don't need the values any longer (code in closures only uses its types for compile-time inference)
                    Context.CodeWriter.Write(localToClear.VariableName);
                    Context.CodeWriter.WriteLine(" = default;");
                }

                typeInferenceCaptureScope.Value.Dispose();
            }
        }
    }

    public override void WriteComponentTypeInferenceMethod(ComponentTypeInferenceMethodIntermediateNode node)
    {
        WriteComponentTypeInferenceMethod(node, returnComponentType: false, allowNameof: true);
    }

    private void WriteTypeInferenceMethodParameterInnards(TypeInferenceMethodParameter parameter)
    {
        switch (parameter.Source)
        {
            case ComponentAttributeIntermediateNode attribute:
                // Don't type check generics, since we can't actually write the type name.
                // The type checking will happen anyway since we defined a method and we're generating
                // a call to it.
                WriteComponentAttributeInnards(attribute, canTypeCheck: false);
                break;
            case SplatIntermediateNode splat:
                WriteSplatInnards(splat, canTypeCheck: false);
                break;
            case ComponentChildContentIntermediateNode childNode:
                WriteComponentChildContentInnards(childNode);
                break;
            case SetKeyIntermediateNode setKey:
                WriteSetKeyInnards(setKey);
                break;
            case ReferenceCaptureIntermediateNode capture:
                WriteReferenceCaptureInnards(capture, shouldTypeCheck: false);
                break;
            case CascadingGenericTypeParameter syntheticArg:
                // The value should be populated before we use it, because we emit code for creating ancestors
                // first, and that's where it's populated. However if this goes wrong somehow, we don't want to
                // throw, so use a fallback
                var valueExpression = syntheticArg.ValueExpression ?? "default";
                Context.CodeWriter.Write(valueExpression);
                if (!Context.Options.SuppressNullabilityEnforcement && IsDefaultExpression(valueExpression))
                {
                    Context.CodeWriter.Write("!");
                }
                break;
            case TypeInferenceCapturedVariable capturedVariable:
                Context.CodeWriter.Write(capturedVariable.VariableName);
                break;
            case RenderModeIntermediateNode renderMode:
                WriteCSharpCode(new CSharpCodeIntermediateNode() { Source = renderMode.Source, Children = { renderMode.Children[0] } });
                break;
            default:
                throw new InvalidOperationException($"Not implemented: type inference method parameter from source {parameter.Source}");
        }
    }

    public override void WriteComponentAttribute(ComponentAttributeIntermediateNode node)
    {
        if (node.IsDesignTimePropertyAccessHelper)
        {
            WriteDesignTimePropertyAccessor(node);
            return;
        }

        var addAttributeMethod = node.AddAttributeMethodName ?? GetAddComponentParameterMethodName();

        // _builder.AddComponentParameter(1, nameof(Component.Property), 42);
        Context.CodeWriter.Write(_scopeStack.BuilderVarName);
        Context.CodeWriter.Write(".");
        Context.CodeWriter.Write(addAttributeMethod);
        Context.CodeWriter.Write("(");
        Context.CodeWriter.Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture));
        Context.CodeWriter.Write(", ");

        WriteComponentAttributeName(node);
        Context.CodeWriter.Write(", ");

        if (addAttributeMethod == ComponentsApi.RenderTreeBuilder.AddAttribute)
        {
            Context.CodeWriter.Write("(object)(");
        }

        WriteComponentAttributeInnards(node, canTypeCheck: true);

        if (addAttributeMethod == ComponentsApi.RenderTreeBuilder.AddAttribute)
        {
            Context.CodeWriter.Write(")");
        }

        Context.CodeWriter.Write(");");
        Context.CodeWriter.WriteLine();
    }

    private void WriteDesignTimePropertyAccessor(ComponentAttributeIntermediateNode attribute)
    {
        // These attributes don't really exist in the emitted code, but have a representation in the razor document.
        // We emit a small piece of empty code that is elided by the compiler, so that the IDE has something to reference
        // for Find All References etc.
        Debug.Assert(attribute.BoundAttribute?.ContainingType is not null);
        Context.CodeWriter.Write(" _ = ");
        WriteComponentAttributeName(attribute);
        Context.CodeWriter.WriteLine(";");
    }

    private void WriteComponentAttributeInnards(ComponentAttributeIntermediateNode node, bool canTypeCheck)
    {
        if (node.Children.Count > 1)
        {
            Debug.Assert(node.HasDiagnostics, "We should have reported an error for mixed content.");
            // We render the children anyway, so tooling works.
        }

        if (node.AttributeStructure == AttributeStructure.Minimized)
        {
            // Minimized attributes always map to 'true'
            Context.CodeWriter.Write("true");
        }
        else if (node.Children is [HtmlContentIntermediateNode htmlNode])
        {
            // This is how string attributes are lowered by default, a single HTML node with a single HTML token.
            var htmlTokens = htmlNode.FindDescendantNodes<HtmlIntermediateToken>();
            var content = string.Join(string.Empty, htmlTokens.Select(t => t.Content));
            Context.CodeWriter.WriteStringLiteral(content);
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
                    Context.CodeWriter.Write("(");
                    WriteGloballyQualifiedTypeName(node);
                    Context.CodeWriter.Write(")");
                    Context.CodeWriter.Write("(");
                }

                WriteCSharpTokens(tokens);

                if (canTypeCheck)
                {
                    Context.CodeWriter.Write(")");
                }
            }
            else if (node.BoundAttribute?.IsEventCallbackProperty() ?? false)
            {
                var explicitType = node.HasExplicitTypeName;
                var isInferred = node.IsOpenGeneric;
                if (canTypeCheck && NeedsTypeCheck(node))
                {
                    Context.CodeWriter.Write(ComponentsApi.RuntimeHelpers.TypeCheck);
                    Context.CodeWriter.Write("<");
                    QualifyEventCallback(Context.CodeWriter, node.TypeName, explicitType);
                    Context.CodeWriter.Write(">");
                    Context.CodeWriter.Write("(");
                }

                // Microsoft.AspNetCore.Components.EventCallback.Factory.Create(this, ...) OR
                // Microsoft.AspNetCore.Components.EventCallback.Factory.Create<T>(this, ...)

                Context.CodeWriter.Write("global::");
                Context.CodeWriter.Write(ComponentsApi.EventCallback.FactoryAccessor);
                Context.CodeWriter.Write(".");
                Context.CodeWriter.Write(ComponentsApi.EventCallbackFactory.CreateMethod);

                if (isInferred != true && node.TryParseEventCallbackTypeArgument(out ReadOnlyMemory<char> argument))
                {
                    Context.CodeWriter.Write("<");
                    if (explicitType)
                    {
                        Context.CodeWriter.Write(argument);
                    }
                    else
                    {
                        TypeNameHelper.WriteGloballyQualifiedName(Context.CodeWriter, argument);
                    }

                    Context.CodeWriter.Write(">");
                }

                Context.CodeWriter.Write("(");
                Context.CodeWriter.Write("this");
                Context.CodeWriter.Write(", ");

                WriteCSharpTokens(tokens);

                Context.CodeWriter.Write(")");

                if (canTypeCheck && NeedsTypeCheck(node))
                {
                    Context.CodeWriter.Write(")");
                }
            }
            else
            {
                if (canTypeCheck && NeedsTypeCheck(node))
                {
                    Context.CodeWriter.Write(ComponentsApi.RuntimeHelpers.TypeCheck);
                    Context.CodeWriter.Write("<");
                    WriteGloballyQualifiedTypeName(node);
                    Context.CodeWriter.Write(">");
                    Context.CodeWriter.Write("(");
                }

                WriteCSharpTokens(tokens);

                if (canTypeCheck && NeedsTypeCheck(node))
                {
                    Context.CodeWriter.Write(")");
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

        static bool NeedsTypeCheck(ComponentAttributeIntermediateNode n)
        {
            return n.BoundAttribute != null && !n.BoundAttribute.IsWeaklyTyped();
        }
    }

    private static ImmutableArray<CSharpIntermediateToken> GetCSharpTokens(IntermediateNode node)
    {
        return node.FindDescendantNodes<CSharpIntermediateToken>();
    }

    public override void WriteComponentChildContent(ComponentChildContentIntermediateNode node)
    {
        // Writes something like:
        //
        // _builder.AddComponentParameter(1, "ChildContent", (RenderFragment)((__builder73) => { ... }));
        // OR
        // _builder.AddComponentParameter(1, "ChildContent", (RenderFragment<Person>)((person) => (__builder73) => { ... }));
        BeginWriteAttribute(node.AttributeName);
        Context.CodeWriter.WriteParameterSeparator();
        Context.CodeWriter.Write("(");
        WriteGloballyQualifiedTypeName(node);
        Context.CodeWriter.Write(")(");

        WriteComponentChildContentInnards(node);

        Context.CodeWriter.Write(")");
        Context.CodeWriter.WriteEndMethodInvocation();
    }

    private void WriteComponentChildContentInnards(ComponentChildContentIntermediateNode node)
    {
        // Writes something like:
        //
        // ((__builder73) => { ... })
        // OR
        // ((person) => (__builder73) => { })
        _scopeStack.OpenComponentScope(
            Context,
            node.AttributeName,
            node.IsParameterized ? node.ParameterName : null);
        for (var i = 0; i < node.Children.Count; i++)
        {
            Context.RenderNode(node.Children[i]);
        }
        _scopeStack.CloseScope(Context);
    }

    public override void WriteComponentTypeArgument(ComponentTypeArgumentIntermediateNode node)
    {
        WriteCSharpToken(node.Value);
    }

    public override void WriteTemplate(TemplateIntermediateNode node)
    {
        // Looks like:
        //
        // (__builder73) => { ... }
        _scopeStack.OpenTemplateScope(Context);
        Context.RenderChildren(node);
        _scopeStack.CloseScope(Context);
    }

    public override void WriteSetKey(SetKeyIntermediateNode node)
    {
        // Looks like:
        //
        // _builder.SetKey(_keyValue);

        var codeWriter = Context.CodeWriter;

        codeWriter
            .WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{ComponentsApi.RenderTreeBuilder.SetKey}");
        WriteSetKeyInnards(node);
        codeWriter.WriteEndMethodInvocation();
    }

    private void WriteSetKeyInnards(SetKeyIntermediateNode node)
    {
        WriteCSharpCode(new CSharpCodeIntermediateNode
        {
            Source = node.Source,
            Children =
                    {
                        node.KeyValueToken
                    }
        });
    }

    public override void WriteSplat(SplatIntermediateNode node)
    {
        // Looks like:
        //
        // _builder.AddMultipleAttributes(2, ...);
        Context.CodeWriter.WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{ComponentsApi.RenderTreeBuilder.AddMultipleAttributes}");
        Context.CodeWriter.Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture));
        Context.CodeWriter.WriteParameterSeparator();

        WriteSplatInnards(node, canTypeCheck: true);

        Context.CodeWriter.WriteEndMethodInvocation();
    }

    private void WriteSplatInnards(SplatIntermediateNode node, bool canTypeCheck)
    {
        if (canTypeCheck)
        {
            Context.CodeWriter.Write(ComponentsApi.RuntimeHelpers.TypeCheck);
            Context.CodeWriter.Write("<");
            Context.CodeWriter.Write(ComponentsApi.AddMultipleAttributesTypeFullName);
            Context.CodeWriter.Write(">");
            Context.CodeWriter.Write("(");
        }

        var tokens = GetCSharpTokens(node);
        WriteCSharpTokens(tokens);

        if (canTypeCheck)
        {
            Context.CodeWriter.Write(")");
        }
    }

    public sealed override void WriteFormName(FormNameIntermediateNode node)
    {
        if (node.Children.Count > 1)
        {
            Debug.Assert(node.HasDiagnostics, "We should have reported an error for mixed content.");
        }

        // string __formName = expression;
        Context.CodeWriter.Write("string ");
        Context.CodeWriter.Write(_scopeStack.FormNameVarName);
        Context.CodeWriter.Write(" = ");
        Context.CodeWriter.Write(ComponentsApi.RuntimeHelpers.TypeCheck);
        Context.CodeWriter.Write("<string>(");
        WriteAttributeValue(node.FindDescendantNodes<IntermediateToken>());
        Context.CodeWriter.Write(")");
        Context.CodeWriter.WriteLine(";");
    }

    public override void WriteReferenceCapture(ReferenceCaptureIntermediateNode node)
    {
        // Looks like:
        //
        // _builder.AddComponentReferenceCapture(2, (__value) = { _field = (MyComponent)__value; });
        // OR
        // _builder.AddElementReferenceCapture(2, (__value) = { _field = (ElementReference)__value; });
        var codeWriter = Context.CodeWriter;

        var methodName = node.IsComponentCapture
            ? ComponentsApi.RenderTreeBuilder.AddComponentReferenceCapture
            : ComponentsApi.RenderTreeBuilder.AddElementReferenceCapture;
        codeWriter
            .WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{methodName}")
            .Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture))
            .WriteParameterSeparator();

        WriteReferenceCaptureInnards(node, shouldTypeCheck: true);

        codeWriter.WriteEndMethodInvocation();
    }

    protected override void WriteReferenceCaptureInnards(ReferenceCaptureIntermediateNode node, bool shouldTypeCheck)
    {
        // Looks like:
        //
        // (__value) = { _field = (MyComponent)__value; }
        // OR
        // (__value) = { _field = (ElementRef)__value; }
        const string refCaptureParamName = "__value";
        using (var lambdaScope = Context.CodeWriter.BuildLambda(refCaptureParamName))
        {
            var typecastIfNeeded = shouldTypeCheck && node.IsComponentCapture ? $"({node.ComponentCaptureTypeName})" : string.Empty;
            WriteCSharpCode(new CSharpCodeIntermediateNode
            {
                Source = node.Source,
                Children =
                    {
                        node.IdentifierToken,
                        NodeFactory.CSharpToken($" = {typecastIfNeeded}{refCaptureParamName};")
                    }
            });
        }
    }

    public override void WriteRenderMode(RenderModeIntermediateNode node)
    {
        // Looks like:
        // global::Microsoft.AspNetCore.Components.IComponentRenderMode __renderMode0 = expression;
        WriteCSharpCode(new CSharpCodeIntermediateNode
        {
            Children =
            {
                NodeFactory.CSharpToken($"global::{ComponentsApi.IComponentRenderMode.FullTypeName} {_scopeStack.RenderModeVarName} = "),
                new CSharpCodeIntermediateNode
                {
                    Source = node.Source,
                    Children = { node.Children[0] }
                },
                NodeFactory.CSharpToken(";")
            }
        });
    }

    private void WriteAttribute(string key, ImmutableArray<IntermediateToken> value)
    {
        BeginWriteAttribute(key);

        if (value.Length > 0)
        {
            Context.CodeWriter.WriteParameterSeparator();
            WriteAttributeValue(value);
        }
        else if (!Context.Options.OmitMinimizedComponentAttributeValues)
        {
            // In version 5+, there's no need to supply a value for a minimized attribute.
            // But for older language versions, minimized attributes were represented as "true".
            Context.CodeWriter.WriteParameterSeparator();
            Context.CodeWriter.WriteBooleanLiteral(true);
        }

        Context.CodeWriter.WriteEndMethodInvocation();
    }

    private void WriteAttribute(IntermediateNode nameExpression, ImmutableArray<IntermediateToken> value)
    {
        BeginWriteAttribute(nameExpression);

        if (value.Length > 0)
        {
            Context.CodeWriter.WriteParameterSeparator();
            WriteAttributeValue(value);
        }

        Context.CodeWriter.WriteEndMethodInvocation();
    }

    protected override void BeginWriteAttribute(string key)
    {
        Context.CodeWriter
            .WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{ComponentsApi.RenderTreeBuilder.AddAttribute}")
            .Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture))
            .WriteParameterSeparator()
            .WriteStringLiteral(key);
    }

    protected override void BeginWriteAttribute(IntermediateNode nameExpression)
    {
        Context.CodeWriter.WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{ComponentsApi.RenderTreeBuilder.AddAttribute}");
        Context.CodeWriter.Write((_sourceSequence++).ToString(CultureInfo.InvariantCulture));
        Context.CodeWriter.WriteParameterSeparator();

        var tokens = GetCSharpTokens(nameExpression);
        WriteCSharpTokens(tokens);
    }

    // There are a few cases here, we need to handle:
    // - Pure HTML
    // - Pure CSharp
    // - Mixed HTML and CSharp
    //
    // Only the mixed case is complicated, we want to turn it into code that will concatenate
    // the values into a string at runtime.

    private void WriteAttributeValue(ImmutableArray<IntermediateToken> tokens)
    {
        if (tokens == null)
        {
            throw new ArgumentNullException(nameof(tokens));
        }

        var writer = Context.CodeWriter;
        var hasHtml = false;
        var hasCSharp = false;
        for (var i = 0; i < tokens.Length; i++)
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
            for (var i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];
                if (token is CSharpIntermediateToken csharpToken)
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

                    WriteCSharpToken(csharpToken);
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
                WriteCSharpToken((CSharpIntermediateToken)token);
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

    private void WriteCSharpTokens(ImmutableArray<CSharpIntermediateToken> tokens)
    {
        foreach (var token in tokens)
        {
            WriteCSharpToken(token);
        }
    }

    private void WriteCSharpToken(CSharpIntermediateToken token)
    {
        if (token.Source?.FilePath == null)
        {
            Context.CodeWriter.Write(token.ContentParts);
            return;
        }

        using (Context.BuildEnhancedLinePragma(token.Source))
        {
            Context.CodeWriter.Write(token.ContentParts);
        }
    }
}
