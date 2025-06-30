// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Components;

// Based on the DesignTimeNodeWriter from Razor repo.
internal class ComponentDesignTimeNodeWriter(CodeRenderingContext context, RazorLanguageVersion version)
    : ComponentNodeWriter(context, version)
{
    private readonly ScopeStack _scopeStack = new ScopeStack();

    private const string DesignTimeVariable = "__o";

    // Avoid using `AddComponentParameter` in design time where we currently don't detect its availability.
    protected override bool CanUseAddComponentParameter() => false;

    public override void WriteMarkupBlock(MarkupBlockIntermediateNode node)
    {
        // Do nothing
    }

    public override void WriteMarkupElement(MarkupElementIntermediateNode node)
    {
        Context.RenderChildren(node);
    }

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
        WriteCSharpExpressionInnards(node);
    }

    private void WriteCSharpExpressionInnards(CSharpExpressionIntermediateNode node, string? type = null)
    {
        if (node.Children.Count == 0)
        {
            return;
        }

        if (node.Source != null)
        {
            using (Context.CodeWriter.BuildLinePragma(node.Source.Value, Context))
            {
                var offset = DesignTimeVariable.Length + " = ".Length;

                if (type != null)
                {
                    offset += type.Length + 2; // two parenthesis
                }

                Context.CodeWriter.WritePadding(offset, node.Source, Context);
                Context.CodeWriter.WriteStartAssignment(DesignTimeVariable);

                if (type != null)
                {
                    Context.CodeWriter.Write("(");
                    TypeNameHelper.WriteGloballyQualifiedName(Context.CodeWriter, type);
                    Context.CodeWriter.Write(")");
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
                        // There may be something else inside the expression like a Template or another extension node.
                        Context.RenderNode(node.Children[i]);
                    }
                }

                Context.CodeWriter.WriteLine(";");
            }
        }
        else
        {
            Context.CodeWriter.WriteStartAssignment(DesignTimeVariable);
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

        IDisposable? linePragmaScope = null;
        if (node.Source != null)
        {
            if (!isWhitespaceStatement)
            {
                linePragmaScope = Context.CodeWriter.BuildLinePragma(node.Source.Value, Context);
            }

            Context.CodeWriter.WritePadding(0, node.Source.Value, Context);
        }
        else if (isWhitespaceStatement)
        {
            // Don't write whitespace if there is no line mapping for it.
            return;
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
        // This expression may contain code so we have to render it or else the design-time
        // exprience is broken.
        if (node.AttributeNameExpression is CSharpExpressionIntermediateNode expression)
        {
            WriteCSharpExpressionInnards(expression, "string");
        }

        Context.RenderChildren(node);
    }

    public override void WriteHtmlAttributeValue(HtmlAttributeValueIntermediateNode node)
    {
        // Do nothing, this can't contain code.
    }

    public override void WriteCSharpExpressionAttributeValue(CSharpExpressionAttributeValueIntermediateNode node)
    {
        if (node.Children.Count == 0)
        {
            return;
        }

        Context.CodeWriter.WriteStartAssignment(DesignTimeVariable);
        for (var i = 0; i < node.Children.Count; i++)
        {
            if (node.Children[i] is CSharpIntermediateToken token)
            {
                WriteCSharpToken(token);
            }
            else
            {
                // There may be something else inside the expression like a Template or another extension node.
                Context.RenderNode(node.Children[i]);
            }
        }

        Context.CodeWriter.WriteLine(";");
    }

    public override void WriteHtmlContent(HtmlContentIntermediateNode node)
    {
        // Do nothing
    }

    protected override void BeginWriteAttribute(string key)
    {
        Context.CodeWriter
            .WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{nameof(ComponentsApi.RenderTreeBuilder.AddAttribute)}")
            .Write("-1")
            .WriteParameterSeparator()
            .WriteStringLiteral(key);
    }

    protected override void BeginWriteAttribute(IntermediateNode expression)
    {
        Context.CodeWriter.WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{ComponentsApi.RenderTreeBuilder.AddAttribute}");
        Context.CodeWriter.Write("-1");
        Context.CodeWriter.WriteParameterSeparator();

        var tokens = GetCSharpTokens(expression);
        WriteCSharpTokens(tokens);
    }

    public override void WriteComponent(ComponentIntermediateNode node)
    {
        // We might need a scope for inferring types,
        CodeWriterExtensions.CSharpCodeWritingScope? typeInferenceCaptureScope = null;
        string? typeInferenceLocalName = null;

        var suppressTypeInference = ShouldSuppressTypeInferenceCall(node);
        if (suppressTypeInference)
        {
        }
        else if (node.TypeInferenceNode == null)
        {
            // Writes something like:
            //
            // __builder.OpenComponent<MyComponent>(0);
            // __builder.AddAttribute(1, "Foo", ...);
            // __builder.AddAttribute(2, "ChildContent", ...);
            // __builder.SetKey(someValue);
            // __builder.AddElementCapture(3, (__value) => _field = __value);
            // __builder.CloseComponent();

            foreach (var typeArgument in node.TypeArguments)
            {
                Context.RenderNode(typeArgument);
            }

            // We need to preserve order for attributes and attribute splats since the ordering
            // has a semantic effect.

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
                    Context.RenderNode(renderMode);
                }
            }

            if (node.ChildContents.Any())
            {
                foreach (var childContent in node.ChildContents)
                {
                    Context.RenderNode(childContent);
                }
            }
            else
            {
                // We eliminate 'empty' child content when building the tree so that usage like
                // '<MyComponent>\r\n</MyComponent>' doesn't create a child content.
                //
                // Consider what would happen if the user's cursor was inside the element. At
                // design -time we want to render an empty lambda to provide proper scoping
                // for any code that the user types.
                Context.RenderNode(new ComponentChildContentIntermediateNode()
                {
                    TypeName = ComponentsApi.RenderFragment.FullTypeName,
                });
            }

            foreach (var setKey in node.SetKeys)
            {
                Context.RenderNode(setKey);
            }

            foreach (var capture in node.Captures)
            {
                Context.RenderNode(capture);
            }
        }
        else
        {
            var parameters = GetTypeInferenceMethodParameters(node.TypeInferenceNode);

            // If this component is going to cascade any of its generic types, we have to split its type inference
            // into two parts. First we call an inference method that captures all the parameters in local variables,
            // then we use those to call the real type inference method that emits the component. The reason for this
            // is so the captured variables can be used by descendants without re-evaluating the expressions.
            if (node.Component.SuppliesCascadingGenericParameters())
            {
                typeInferenceCaptureScope = Context.CodeWriter.BuildScope();
                Context.CodeWriter.Write("global::");
                Context.CodeWriter.Write(node.TypeInferenceNode.FullTypeName);
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
            // __Blazor.MyComponent.TypeInference.CreateMyComponent_0(__builder, 0, 1, ..., 2, ..., 3, ....);

            // We don't need an instance of this component, but having its type information is useful later for allowing
            // Roslyn to bind to properties that represent component attributes.
            // It's a bit silly that this variable will be called __typeInference_CreateMyComponent_0 with "Create" in the
            // name, but since we've already done the work to create a unique name, we should reuse it.

            typeInferenceLocalName = $"__typeInference_{node.TypeInferenceNode.MethodName}";

            Context.CodeWriter.Write("var ");
            Context.CodeWriter.Write(typeInferenceLocalName);
            Context.CodeWriter.Write(" = ");

            Context.CodeWriter.Write("global::");
            Context.CodeWriter.Write(node.TypeInferenceNode.FullTypeName);
            Context.CodeWriter.Write(".");
            Context.CodeWriter.Write(node.TypeInferenceNode.MethodName);
            Context.CodeWriter.Write("(");

            Context.CodeWriter.Write(_scopeStack.BuilderVarName);
            Context.CodeWriter.Write(", ");

            Context.CodeWriter.Write("-1");

            foreach (var parameter in parameters)
            {
                Context.CodeWriter.Write(", ");

                if (!string.IsNullOrEmpty(parameter.SeqName))
                {
                    Context.CodeWriter.Write("-1");
                    Context.CodeWriter.Write(", ");
                }

                WriteTypeInferenceMethodParameterInnards(parameter);
            }

            Context.CodeWriter.Write(");");
            Context.CodeWriter.WriteLine();
        }

        // We need to write property access here in case we're in a scope for capturing types, because we need to re-use
        // the type inference local for accessing property names.
        // We also need to disable BL0005, which is an analyzer provided by the runtime that will warn if a component
        // parameter is explicitly set, but that's exactly what we will be doing in order to represent the attribute
        // being set.

        if (!suppressTypeInference)
        {
            var wrotePragmaDisable = false;
            foreach (var child in node.Children)
            {
                if (child is ComponentAttributeIntermediateNode attribute)
                {
                    WritePropertyAccess(attribute, node, typeInferenceLocalName, shouldWriteBL0005Disable: !wrotePragmaDisable, out var wrotePropertyAccess);

                    if (wrotePropertyAccess)
                    {
                        wrotePragmaDisable = true;
                    }
                }
            }

            if (wrotePragmaDisable)
            {
                // Restore the warning in case the user has written other code that explicitly sets a property
                Context.CodeWriter.WriteLine("#pragma warning restore BL0005");
            }
        }

        typeInferenceCaptureScope?.Dispose();

        // We want to generate something that references the Component type to avoid
        // the "usings directive is unnecessary" message.
        // Looks like:
        // __o = typeof(SomeNamespace.SomeComponent);
        using (Context.CodeWriter.BuildLinePragma(node.Source.AssumeNotNull(), Context))
        {
            Context.CodeWriter.Write(DesignTimeVariable);
            Context.CodeWriter.Write(" = ");
            Context.CodeWriter.Write("typeof(");
            Context.CodeWriter.Write("global::");
            if (!node.Component.IsGenericTypedComponent())
            {
                Context.CodeWriter.Write(node.Component.Name);
            }
            else
            {
                // The tags can be unqualified or fully qualified, the TagName always equals
                // the class name so we rely on that to compute the globally fully qualified
                // type name
                if (!node.TagName.Contains("."))
                {
                    // The tag is not fully qualified
                    Context.CodeWriter.Write(node.Component.GetTypeNamespace());
                    Context.CodeWriter.Write(".");
                }

                Context.CodeWriter.Write(node.TagName);
                Context.CodeWriter.Write("<");
                var typeArgumentCount = node.Component.GetTypeParameters().Count();
                for (var i = 1; i < typeArgumentCount; i++)
                {
                    Context.CodeWriter.Write(",");
                }

                Context.CodeWriter.Write(">");
            }

            Context.CodeWriter.Write(");");
            Context.CodeWriter.WriteLine();
        }
    }

    public override void WriteComponentTypeInferenceMethod(ComponentTypeInferenceMethodIntermediateNode node)
    {
        base.WriteComponentTypeInferenceMethod(node, returnComponentType: true, allowNameof: false);
    }

    private void WriteTypeInferenceMethodParameterInnards(TypeInferenceMethodParameter parameter)
    {
        switch (parameter.Source)
        {
            case ComponentAttributeIntermediateNode attribute:
                // Don't type check generics, since we can't actually write the type name.
                // The type checking with happen anyway since we defined a method and we're generating
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
        // This attribute might only be here in order to allow us to generate code in WritePropertyAccess
        if (node.IsDesignTimePropertyAccessHelper)
        {
            return;
        }

        // Looks like:
        // __o = 17;
        Context.CodeWriter.Write(DesignTimeVariable);
        Context.CodeWriter.Write(" = ");

        // Following the same design pattern as the runtime codegen
        WriteComponentAttributeInnards(node, canTypeCheck: true);

        Context.CodeWriter.Write(";");
        Context.CodeWriter.WriteLine();
    }

    private void WritePropertyAccess(
        ComponentAttributeIntermediateNode node,
        ComponentIntermediateNode componentNode,
        string? typeInferenceLocalName,
        bool shouldWriteBL0005Disable,
        out bool wrotePropertyAccess)
    {
        wrotePropertyAccess = false;
        if (node?.TagHelper?.Name is null || node.OriginalAttributeSpan is null)
        {
            return;
        }

        if (node.BoundAttribute.Metadata.TryGetValue(ComponentMetadata.Component.InitOnlyProperty, out var isInitOnlyValue) &&
            bool.TryParse(isInitOnlyValue, out var isInitOnly) &&
            isInitOnly)
        {
            // If a component property is init only then the code we generate for it won't compile.
            return;
        }

        // Write the name of the property, for rename support.
        // __o = ((global::ComponentName)default).PropertyName;
        var originalAttributeName = node.OriginalAttributeName ?? node.AttributeName;

        int offset;
        if (originalAttributeName == node.PropertyName)
        {
            offset = 0;
        }
        else if (originalAttributeName.StartsWith($"@bind-{node.PropertyName}", StringComparison.Ordinal))
        {
            offset = 5;
        }
        else
        {
            return;
        }

        if (shouldWriteBL0005Disable)
        {
            Context.CodeWriter.WriteLine("#pragma warning disable BL0005");
        }

        var attributeSourceSpan = (SourceSpan)node.OriginalAttributeSpan;
        attributeSourceSpan = new SourceSpan(attributeSourceSpan.FilePath, attributeSourceSpan.AbsoluteIndex + offset, attributeSourceSpan.LineIndex, attributeSourceSpan.CharacterIndex + offset, node.PropertyName.Length, attributeSourceSpan.LineCount, attributeSourceSpan.CharacterIndex + offset + node.PropertyName.Length);

        if (componentNode.TypeInferenceNode == null)
        {
            Context.CodeWriter.Write("((");
            TypeNameHelper.WriteGloballyQualifiedName(Context.CodeWriter, componentNode.TypeName);
            Context.CodeWriter.Write(")default)");
        }
        else
        {
            if (typeInferenceLocalName is null)
            {
                throw new InvalidOperationException("No type inference local name was supplied, but type inference is required to reference a component type.");
            }

            // Earlier when we did the type inference stuff, we captured a variable which the compiler would know the type information
            // for explicitly for the purposes of using it now
            Context.CodeWriter.Write(typeInferenceLocalName);
        }

        Context.CodeWriter.Write(".");
        Context.CodeWriter.WriteLine();

        using (Context.CodeWriter.BuildLinePragma(attributeSourceSpan, Context))
        {
            Context.CodeWriter.WritePadding(0, attributeSourceSpan, Context);
            Context.CodeWriter.WriteIdentifierEscapeIfNeeded(node.PropertyName);
            Context.AddSourceMappingFor(attributeSourceSpan);
            Context.CodeWriter.WriteLine(node.PropertyName);
        }

        Context.CodeWriter.Write(" = default;");
        Context.CodeWriter.WriteLine();

        wrotePropertyAccess = true;
    }

    private void WriteComponentAttributeInnards(ComponentAttributeIntermediateNode node, bool canTypeCheck)
    {
        if (node.Children.Count > 1)
        {
            Debug.Assert(node.HasDiagnostics, "We should have reported an error for mixed content.");
            // We render the children anyway, so tooling works.
        }

        // We limit component attributes to simple cases. However there is still a lot of complexity
        // to handle here, since there are a few different cases for how an attribute might be structured.
        //
        // This roughly follows the design of the runtime writer for simplicity.
        if (node.AttributeStructure == AttributeStructure.Minimized)
        {
            // Minimized attributes always map to 'true'
            Context.CodeWriter.Write("true");
        }
        else if (node.Children.Count == 1 && node.Children[0] is HtmlContentIntermediateNode)
        {
            // We don't actually need the content at designtime, an empty string will do.
            Context.CodeWriter.Write("\"\"");
        }
        else
        {
            // There are a few different forms that could be used to contain all of the tokens, but we don't really care
            // exactly what it looks like - we just want all of the content.
            //
            // This can include an empty list in some cases like the following (sic):
            //      <MyComponent Value="
            //
            // Or a CSharpExpressionIntermediateNode when the attribute has an explicit transition like:
            //      <MyComponent Value="@value" />
            //
            // Of a list of tokens directly in the attribute.
            var tokens = GetCSharpTokens(node);

            if ((node.BoundAttribute?.IsDelegateProperty() ?? false) ||
                (node.BoundAttribute?.IsChildContentProperty() ?? false))
            {
                // We always surround the expression with the delegate constructor. This makes type
                // inference inside lambdas, and method group conversion do the right thing.
                if (canTypeCheck)
                {
                    Context.CodeWriter.Write("new ");
                    WriteGloballyQualifiedTypeName(node);
                    Context.CodeWriter.Write("(");
                }

                Context.CodeWriter.WriteLine();

                WriteCSharpTokens(tokens);

                if (canTypeCheck)
                {
                    Context.CodeWriter.Write(")");
                }
            }
            else if (node.BoundAttribute?.IsEventCallbackProperty() ?? false)
            {
                // This is the case where we are writing an EventCallback (a delegate with super-powers).
                //
                // An event callback can either be passed verbatim, or it can be created by the EventCallbackFactory.
                // Since we don't look at the code the user typed inside the attribute value, this is always
                // resolved via overloading.
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

                Context.CodeWriter.WriteLine();

                WriteCSharpTokens(tokens);

                Context.CodeWriter.Write(")");

                if (canTypeCheck && NeedsTypeCheck(node))
                {
                    Context.CodeWriter.Write(")");
                }
            }
            else
            {
                // This is the case when an attribute contains C# code
                //
                // If we have a parameter type, then add a type check.
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
            // Weakly typed attributes will have their TypeName set to null.
            return n.BoundAttribute != null && n.TypeName != null;
        }
    }

    private static ImmutableArray<CSharpIntermediateToken> GetCSharpTokens(IntermediateNode node)
    {
        // We generally expect all children to be CSharp, this is here just in case.
        return node.FindDescendantNodes<CSharpIntermediateToken>();
    }

    public override void WriteComponentChildContent(ComponentChildContentIntermediateNode node)
    {
        // Writes something like:
        //
        // __builder.AddAttribute(1, "ChildContent", (RenderFragment)((__builder73) => { ... }));
        // OR
        // __builder.AddAttribute(1, "ChildContent", (RenderFragment<Person>)((person) => (__builder73) => { ... }));
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
        // At design type we want write the equivalent of:
        //
        // __o = typeof(TItem);
        Context.CodeWriter.Write(DesignTimeVariable);
        Context.CodeWriter.Write(" = ");
        Context.CodeWriter.Write("typeof(");

        var tokens = GetCSharpTokens(node);
        WriteCSharpTokens(tokens);

        Context.CodeWriter.Write(");");
        Context.CodeWriter.WriteLine();
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
        // __builder.SetKey(_keyValue);

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
        // __builder.AddMultipleAttributes(2, ...);
        Context.CodeWriter.WriteStartMethodInvocation($"{_scopeStack.BuilderVarName}.{ComponentsApi.RenderTreeBuilder.AddMultipleAttributes}");
        Context.CodeWriter.Write("-1");
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

        foreach (var token in GetCSharpTokens(node))
        {
            Context.CodeWriter.Write(ComponentsApi.RuntimeHelpers.TypeCheck);
            Context.CodeWriter.Write("<string>(");
            WriteCSharpToken(token);
            Context.CodeWriter.WriteLine(");");
        }
    }

    public override void WriteReferenceCapture(ReferenceCaptureIntermediateNode node)
    {
        // Looks like:
        //
        // __field = default(MyComponent);
        WriteReferenceCaptureInnards(node, shouldTypeCheck: true);
    }

    protected override void WriteReferenceCaptureInnards(ReferenceCaptureIntermediateNode node, bool shouldTypeCheck)
    {
        // We specialize this code based on whether or not we can type check. When we're calling into
        // a type-inferenced component, we can't do the type check. See the comments in WriteTypeInferenceMethod.
        if (shouldTypeCheck)
        {
            // The runtime node writer moves the call elsewhere. At design time we
            // just want sufficiently similar code that any unknown-identifier or type
            // errors will be equivalent
            var captureTypeName = node.IsComponentCapture
                ? TypeNameHelper.GetGloballyQualifiedNameIfNeeded(node.ComponentCaptureTypeName)
                : ComponentsApi.ElementReference.FullTypeName;
            var nullSuppression = !Context.Options.SuppressNullabilityEnforcement ? "!" : string.Empty;
            WriteCSharpCode(new CSharpCodeIntermediateNode
            {
                Source = node.Source,
                Children =
                    {
                        node.IdentifierToken,
                        NodeFactory.CSharpToken($" = default({captureTypeName}){nullSuppression};")
                    }
            });
        }
        else
        {
            // Looks like:
            //
            // (__value) = { _field = (MyComponent)__value; }
            // OR
            // (__value) = { _field = (ElementRef)__value; }
            const string refCaptureParamName = "__value";
            using (var lambdaScope = Context.CodeWriter.BuildLambda(refCaptureParamName))
            {
                WriteCSharpCode(new CSharpCodeIntermediateNode
                {
                    Source = node.Source,
                    Children =
                        {
                            node.IdentifierToken,
                            NodeFactory.CSharpToken($" = {refCaptureParamName};")
                        }
                });
            }
        }
    }

    public override void WriteRenderMode(RenderModeIntermediateNode node)
    {
        // Looks like:
        // __o = (global::Microsoft.AspNetCore.Components.IComponentRenderMode)(expression);
        WriteCSharpCode(new CSharpCodeIntermediateNode
        {
            Children =
            {
                NodeFactory.CSharpToken($"{DesignTimeVariable} = (global::{ComponentsApi.IComponentRenderMode.FullTypeName})("),
                new CSharpCodeIntermediateNode
                {
                    Source = node.Source,
                    Children = { node.Children[0] }
                },
                NodeFactory.CSharpToken(");")
            }
        });
    }

    private void WriteCSharpTokens(ImmutableArray<CSharpIntermediateToken> tokens)
    {
        foreach (var token in tokens)
        {
            WriteCSharpToken(token);
        }
    }

    private void WriteCSharpToken(IntermediateToken token)
    {
        if (string.IsNullOrWhiteSpace(token.Content))
        {
            return;
        }

        if (token.Source?.FilePath == null)
        {
            Context.CodeWriter.Write(token.Content);
            return;
        }

        using (Context.CodeWriter.BuildLinePragma(token.Source, Context))
        {
            Context.CodeWriter.WritePadding(0, token.Source.Value, Context);
            Context.AddSourceMappingFor(token);
            Context.CodeWriter.Write(token.Content);
        }
    }
}
