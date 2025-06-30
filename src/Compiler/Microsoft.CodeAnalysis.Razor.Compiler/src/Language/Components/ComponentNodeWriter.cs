// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Components;

internal abstract class ComponentNodeWriter(CodeRenderingContext context, RazorLanguageVersion version)
    : IntermediateNodeWriter(context)
{
    private readonly RazorLanguageVersion _version = version;

    protected virtual bool CanUseAddComponentParameter()
    {
        return !Context.Options.SuppressAddComponentParameter && _version >= RazorLanguageVersion.Version_8_0;
    }

    protected string GetAddComponentParameterMethodName()
    {
        return CanUseAddComponentParameter()
            ? ComponentsApi.RenderTreeBuilder.AddComponentParameter
            : ComponentsApi.RenderTreeBuilder.AddAttribute;
    }

    protected abstract void BeginWriteAttribute(string key);

    protected abstract void BeginWriteAttribute(IntermediateNode expression);

    protected abstract void WriteReferenceCaptureInnards(ReferenceCaptureIntermediateNode node, bool shouldTypeCheck);

    public abstract void WriteTemplate(TemplateIntermediateNode node);

    public sealed override void BeginWriterScope(string writer)
        => throw new NotImplementedException(nameof(BeginWriterScope));

    public sealed override void EndWriterScope()
        => throw new NotImplementedException(nameof(EndWriterScope));

    public sealed override void WriteCSharpCodeAttributeValue(CSharpCodeAttributeValueIntermediateNode node)
    {
        // We used to support syntaxes like <elem onsomeevent=@{ /* some C# code */ } /> but this is no longer the
        // case.
        //
        // We provide an error for this case just to be friendly.
        var content = string.Join("", node.Children.OfType<IntermediateToken>().Select(t => t.Content));
        Context.AddDiagnostic(ComponentDiagnosticFactory.Create_CodeBlockInAttribute(node.Source, content));
        return;
    }

    protected bool ShouldSuppressTypeInferenceCall(ComponentIntermediateNode node)
    {
        // When RZ10001 (type of component cannot be inferred) is reported, we want to suppress the equivalent CS0411 errors,
        // so we don't generate the call to TypeInference.CreateComponent.
        return node.Diagnostics.Any(d => d.Id == ComponentDiagnosticFactory.GenericComponentTypeInferenceUnderspecified.Id);
    }

    protected void WriteComponentTypeInferenceMethod(ComponentTypeInferenceMethodIntermediateNode node, bool returnComponentType, bool allowNameof)
    {
        var parameters = GetTypeInferenceMethodParameters(node);

        // This is really similar to the code in WriteComponentAttribute and WriteComponentChildContent - except simpler because
        // attributes and child contents look like variables.
        //
        // Looks like:
        //
        //  public static void CreateFoo_0<T1, T2>(RenderTreeBuilder __builder, int seq, int __seq0, T1 __arg0, int __seq1, global::System.Collections.Generic.List<T2> __arg1, int __seq2, string __arg2)
        //  {
        //      builder.OpenComponent<Foo<T1, T2>>();
        //      builder.AddComponentParameter(__seq0, nameof(Foo<T1, T2>.Attr0), __arg0);
        //      builder.AddComponentParameter(__seq1, nameof(Foo<T1, T2>.Attr1), __arg1);
        //      builder.AddComponentParameter(__seq2, nameof(Foo<T1, T2>.Attr2), __arg2);
        //      builder.CloseComponent();
        //  }
        //
        // As a special case, we need to generate a thunk for captures in this block instead of using
        // them verbatim.
        //
        // The problem is that RenderTreeBuilder wants an Action<object>. The caller can't write the type
        // name if it contains generics, and we can't write the variable they want to assign to.
        var writer = Context.CodeWriter;

        writer.Write("public static ");
        if (returnComponentType)
        {
            writer.Write(node.Component.TypeName);
        }
        else
        {
            writer.Write("void");
        }
        writer.Write(" ");
        writer.Write(node.MethodName);
        writer.Write("<");
        writer.Write(string.Join(", ", node.Component.Component.GetTypeParameters().Select(serializeTypeParameter)));
        writer.Write(">");

        writer.Write("(");
        writer.Write("global::");
        writer.Write(ComponentsApi.RenderTreeBuilder.FullTypeName);
        writer.Write(" ");
        writer.Write(ComponentsApi.RenderTreeBuilder.BuilderParameter);
        writer.Write(", ");
        writer.Write("int seq");

        if (parameters.Count > 0)
        {
            writer.Write(", ");
        }

        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];

            if (!parameter.SeqName.IsNullOrEmpty())
            {
                writer.Write("int ");
                writer.Write(parameter.SeqName);
                writer.Write(", ");
            }

            writer.Write(parameter.TypeName);
            writer.Write(" ");
            writer.Write(parameter.ParameterName);

            if (i < parameters.Count - 1)
            {
                writer.Write(", ");
            }
        }

        writer.Write(")");

        writeConstraints(writer, node);

        writer.WriteLine("{");

        // _builder.OpenComponent<TComponent>(42);
        Context.CodeWriter.Write(ComponentsApi.RenderTreeBuilder.BuilderParameter);
        Context.CodeWriter.Write(".");
        Context.CodeWriter.Write(ComponentsApi.RenderTreeBuilder.OpenComponent);
        Context.CodeWriter.Write("<");
        Context.CodeWriter.Write(node.Component.TypeName);
        Context.CodeWriter.Write(">(");
        Context.CodeWriter.Write("seq");
        Context.CodeWriter.Write(");");
        Context.CodeWriter.WriteLine();

        string? renderModeParameterName = null;
        foreach (var parameter in parameters)
        {
            switch (parameter.Source)
            {
                case ComponentAttributeIntermediateNode attribute:
                    Context.CodeWriter.WriteStartInstanceMethodInvocation(ComponentsApi.RenderTreeBuilder.BuilderParameter, GetAddComponentParameterMethodName());

                    if (parameter.SeqName is not null)
                    {
                        Context.CodeWriter.Write(parameter.SeqName);
                        Context.CodeWriter.Write(", ");
                    }

                    WriteComponentAttributeName(attribute, allowNameof);
                    Context.CodeWriter.Write(", ");

                    if (!CanUseAddComponentParameter())
                    {
                        Context.CodeWriter.Write("(object)");
                    }

                    Context.CodeWriter.Write(parameter.ParameterName);
                    Context.CodeWriter.WriteEndMethodInvocation();
                    break;

                case SplatIntermediateNode:
                    Context.CodeWriter.WriteStartInstanceMethodInvocation(ComponentsApi.RenderTreeBuilder.BuilderParameter, ComponentsApi.RenderTreeBuilder.AddMultipleAttributes);

                    if (parameter.SeqName is not null)
                    {
                        Context.CodeWriter.Write(parameter.SeqName);
                        Context.CodeWriter.Write(", ");
                    }

                    Context.CodeWriter.Write(parameter.ParameterName);
                    Context.CodeWriter.WriteEndMethodInvocation();
                    break;

                case ComponentChildContentIntermediateNode childContent:
                    Context.CodeWriter.WriteStartInstanceMethodInvocation(ComponentsApi.RenderTreeBuilder.BuilderParameter, GetAddComponentParameterMethodName());

                    if (parameter.SeqName is not null)
                    {
                        Context.CodeWriter.Write(parameter.SeqName);
                        Context.CodeWriter.Write(", ");
                    }

                    Context.CodeWriter.Write($"\"{childContent.AttributeName}\"");
                    Context.CodeWriter.Write(", ");

                    if (!CanUseAddComponentParameter())
                    {
                        Context.CodeWriter.Write("(object)");
                    }

                    Context.CodeWriter.Write(parameter.ParameterName);
                    Context.CodeWriter.WriteEndMethodInvocation();
                    break;

                case SetKeyIntermediateNode:
                    Context.CodeWriter.WriteStartInstanceMethodInvocation(ComponentsApi.RenderTreeBuilder.BuilderParameter, ComponentsApi.RenderTreeBuilder.SetKey);
                    Context.CodeWriter.Write(parameter.ParameterName);
                    Context.CodeWriter.WriteEndMethodInvocation();
                    break;

                case ReferenceCaptureIntermediateNode capture:
                    Context.CodeWriter.WriteStartInstanceMethodInvocation(ComponentsApi.RenderTreeBuilder.BuilderParameter, capture.IsComponentCapture ? ComponentsApi.RenderTreeBuilder.AddComponentReferenceCapture : ComponentsApi.RenderTreeBuilder.AddElementReferenceCapture);

                    if (parameter.SeqName is string seqName)
                    {
                        Context.CodeWriter.Write(seqName);
                    }

                    Context.CodeWriter.Write(", ");

                    var cast = capture.IsComponentCapture ? $"({capture.ComponentCaptureTypeName})" : string.Empty;
                    Context.CodeWriter.Write($"(__value) => {{ {parameter.ParameterName}({cast}__value); }}");
                    Context.CodeWriter.WriteEndMethodInvocation();
                    break;

                case CascadingGenericTypeParameter:
                    // We only use the synthetic cascading parameters for type inference
                    break;

                case RenderModeIntermediateNode:
                    renderModeParameterName = parameter.ParameterName;
                    break;

                default:
                    throw new InvalidOperationException($"Not implemented: type inference method parameter from source {parameter.Source}");
            }
        }

        if (renderModeParameterName is not null)
        {
            WriteAddComponentRenderMode(ComponentsApi.RenderTreeBuilder.BuilderParameter, renderModeParameterName);
        }

        Context.CodeWriter.WriteInstanceMethodInvocation(ComponentsApi.RenderTreeBuilder.BuilderParameter, ComponentsApi.RenderTreeBuilder.CloseComponent);

        if (returnComponentType)
        {
            writer.WriteLine("return default;");
        }

        writer.WriteLine("}");

        if (node.Component.Component.SuppliesCascadingGenericParameters())
        {
            // If this component cascades any generic parameters, we'll need to be able to capture its type inference
            // args at the call site. The point of this is to ensure that:
            //
            // [1] We only evaluate each expression once
            // [2] We evaluate them in the correct order matching the developer's source
            // [3] We can even make variables for lambdas or other expressions that can't just be assigned to implicitly-typed vars.
            //
            // We do that by emitting a method like the following. It has exactly the same generic type inference
            // characteristics as the regular CreateFoo_0 method emitted earlier
            //
            //  public static void CreateFoo_0_CaptureParameters<T1, T2>(T1 __arg0, out T1 __arg0_out, global::System.Collections.Generic.List<T2> __arg1, out global::System.Collections.Generic.List<T2> __arg1_out, int __seq2, string __arg2, out string __arg2_out)
            //  {
            //      __arg0_out = __arg0;
            //      __arg1_out = __arg1;
            //      __arg2_out = __arg2;
            //  }
            //
            writer.WriteLine();
            writer.Write("public static void ");
            writer.Write(node.MethodName);
            writer.Write("_CaptureParameters<");
            writer.Write(string.Join(", ", node.Component.Component.GetTypeParameters().Select(a => a.Name)));
            writer.Write(">");

            writer.Write("(");
            var isFirst = true;
            foreach (var parameter in parameters.Where(p => p.UsedForTypeInference))
            {
                if (isFirst)
                {
                    isFirst = false;
                }
                else
                {
                    writer.Write(", ");
                }

                writer.Write(parameter.TypeName);
                writer.Write(" ");
                writer.Write(parameter.ParameterName);
                writer.Write(", out ");
                writer.Write(parameter.TypeName);
                writer.Write(" ");
                writer.Write(parameter.ParameterName);
                writer.Write("_out");
            }

            writer.Write(")");

            writeConstraints(writer, node);

            writer.WriteLine("{");
            foreach (var parameter in parameters.Where(p => p.UsedForTypeInference))
            {
                writer.Write("    ");
                writer.Write(parameter.ParameterName);
                writer.Write("_out = ");
                writer.Write(parameter.ParameterName);
                writer.WriteLine(";");
            }
            writer.WriteLine("}");
        }

        static void writeConstraints(CodeWriter writer, ComponentTypeInferenceMethodIntermediateNode node)
        {
            // Writes out a list of generic type constraints with indentation
            // public void Foo<T, U>(T t, U u)
            //      where T: new()
            //      where U: Foo, notnull
            foreach (var constraint in node.GenericTypeConstraints)
            {
                writer.WriteLine();
                writer.Indent(writer.CurrentIndent + writer.TabSize);
                writer.Write(constraint);
            }

            writer.WriteLine();
        }

        static string? serializeTypeParameter(BoundAttributeDescriptor attribute)
        {
            if (attribute.Metadata.TryGetValue(ComponentMetadata.Component.TypeParameterWithAttributesKey, out var withAttributes))
            {
                return withAttributes;
            }

            return attribute.Name;
        }
    }

    protected void WriteComponentAttributeName(ComponentAttributeIntermediateNode attribute, bool allowNameof = true)
    {
        if (allowNameof && attribute.BoundAttribute?.ContainingType is string containingType)
        {
            containingType = attribute.ConcreteContainingType ?? containingType;

            // nameof(containingType.PropertyName)
            // This allows things like Find All References to work in the IDE as we have an actual reference to the parameter
            Context.CodeWriter.Write("nameof(");
            TypeNameHelper.WriteGloballyQualifiedName(Context.CodeWriter, containingType);
            Context.CodeWriter.Write(".");

            if (!attribute.IsSynthesized)
            {
                var attributeSourceSpan = attribute.PropertySpan ?? attribute.OriginalAttributeSpan;
                var requiresEscaping = attribute.PropertyName.IdentifierRequiresEscaping();
                using (Context.BuildEnhancedLinePragma(attributeSourceSpan, characterOffset: requiresEscaping ? 1 : 0))
                {
                    Context.CodeWriter.WriteIdentifierEscapeIfNeeded(attribute.PropertyName);
                    Context.CodeWriter.WriteLine(attribute.PropertyName);
                }
            }
            else
            {
                Context.CodeWriter.Write(attribute.PropertyName);
            }

            Context.CodeWriter.Write(")");
        }
        else
        {
            Context.CodeWriter.WriteStringLiteral(attribute.AttributeName);
        }
    }

    protected List<TypeInferenceMethodParameter> GetTypeInferenceMethodParameters(ComponentTypeInferenceMethodIntermediateNode node)
    {
        var p = new List<TypeInferenceMethodParameter>();

        // Preserve order between attributes and splats
        foreach (var child in node.Component.Children)
        {
            if (child is ComponentAttributeIntermediateNode attribute)
            {
                // Some nodes just exist to help with property access at design time, and don't need anything else written
                if (attribute.IsDesignTimePropertyAccessHelper)
                {
                    continue;
                }

                string typeName;
                if (attribute.GloballyQualifiedTypeName != null)
                {
                    typeName = attribute.GloballyQualifiedTypeName;
                }
                else
                {
                    typeName = attribute.TypeName;
                    if (attribute.BoundAttribute != null && !attribute.BoundAttribute.IsGenericTypedProperty())
                    {
                        typeName = typeName.StartsWith("global::", StringComparison.Ordinal) ? typeName : $"global::{typeName}";
                    }
                }

                p.Add(new TypeInferenceMethodParameter($"__seq{p.Count}", typeName, $"__arg{p.Count}", usedForTypeInference: true, attribute));
            }
            else if (child is SplatIntermediateNode splat)
            {
                var typeName = ComponentsApi.AddMultipleAttributesTypeFullName;
                p.Add(new TypeInferenceMethodParameter($"__seq{p.Count}", typeName, $"__arg{p.Count}", usedForTypeInference: false, splat));
            }
            else if (child is RenderModeIntermediateNode renderMode)
            {
                var typeName = ComponentsApi.IComponentRenderMode.FullTypeName;
                p.Add(new TypeInferenceMethodParameter($"__seq{p.Count}", typeName, $"__arg{p.Count}", usedForTypeInference: false, renderMode));
            }
        }

        foreach (var childContent in node.Component.ChildContents)
        {
            var typeName = childContent.TypeName;
            if (childContent.BoundAttribute != null && !childContent.BoundAttribute.IsGenericTypedProperty())
            {
                typeName = childContent.BoundAttribute.GetGloballyQualifiedTypeName();
            }

            Assumed.NotNull(typeName);

            p.Add(new TypeInferenceMethodParameter($"__seq{p.Count}", typeName, $"__arg{p.Count}", usedForTypeInference: false, childContent));
        }

        foreach (var capture in node.Component.SetKeys)
        {
            p.Add(new TypeInferenceMethodParameter($"__seq{p.Count}", "object", $"__arg{p.Count}", usedForTypeInference: false, capture));
        }

        foreach (var capture in node.Component.Captures)
        {
            // The capture type name should already contain the global:: prefix.
            p.Add(new TypeInferenceMethodParameter($"__seq{p.Count}", capture.TypeName, $"__arg{p.Count}", usedForTypeInference: false, capture));
        }

        // Insert synthetic args for cascaded type inference at the start of the list
        // We do this last so that the indices above aren't affected
        if (node.ReceivesCascadingGenericTypes != null)
        {
            var i = 0;
            foreach (var cascadingGenericType in node.ReceivesCascadingGenericTypes)
            {
                p.Insert(i, new TypeInferenceMethodParameter(null, cascadingGenericType.ValueType, $"__syntheticArg{i}", usedForTypeInference: true, cascadingGenericType));
                i++;
            }
        }

        return p;
    }

    protected static void UseCapturedCascadingGenericParameterVariable(ComponentIntermediateNode node, TypeInferenceMethodParameter parameter, string variableName)
    {
        // If this captured variable corresponds to a generic type we want to cascade to
        // descendants, supply that info to descendants
        if (node.ProvidesCascadingGenericTypes != null)
        {
            foreach (var cascadeGeneric in node.ProvidesCascadingGenericTypes.Values)
            {
                if (cascadeGeneric.ValueSourceNode == parameter.Source)
                {
                    cascadeGeneric.ValueExpression = variableName;
                }
            }
        }

        // Since we've now evaluated and captured this expression, use the variable
        // instead of the expression from now on
        parameter.ReplaceSourceWithCapturedVariable(variableName);
    }

    protected static bool IsDefaultExpression(string expression)
    {
        return expression == "default" || expression.StartsWith("default(", StringComparison.Ordinal);
    }

    protected void WriteAddComponentRenderMode(string builderName, string variableName)
    {
        Context.CodeWriter.Write(builderName);
        Context.CodeWriter.Write(".");
        Context.CodeWriter.Write(ComponentsApi.RenderTreeBuilder.AddComponentRenderMode);
        Context.CodeWriter.Write("(");
        Context.CodeWriter.Write(variableName);
        Context.CodeWriter.Write(");");
        Context.CodeWriter.WriteLine();
    }

    protected void WriteGloballyQualifiedTypeName(ComponentAttributeIntermediateNode node)
    {
        if (node.HasExplicitTypeName)
        {
            Context.CodeWriter.Write(node.TypeName);
        }
        else if (node.BoundAttribute?.GetGloballyQualifiedTypeName() is string typeName)
        {
            Context.CodeWriter.Write(typeName);
        }
        else
        {
            TypeNameHelper.WriteGloballyQualifiedName(Context.CodeWriter, node.TypeName);
        }
    }

    protected void WriteGloballyQualifiedTypeName(ComponentChildContentIntermediateNode node)
    {
        if (node.BoundAttribute?.GetGloballyQualifiedTypeName() is string typeName &&
            !node.BoundAttribute.IsGenericTypedProperty())
        {
            Context.CodeWriter.Write(typeName);
        }
        else
        {
            TypeNameHelper.WriteGloballyQualifiedName(Context.CodeWriter, node.TypeName);
        }
    }

    protected class TypeInferenceMethodParameter
    {
        public string? SeqName { get; private set; }
        public string TypeName { get; private set; }
        public string ParameterName { get; private set; }
        public bool UsedForTypeInference { get; private set; }
        public object Source { get; private set; }

        public TypeInferenceMethodParameter(string? seqName, string typeName, string parameterName, bool usedForTypeInference, object source)
        {
            SeqName = seqName;
            TypeName = typeName;
            ParameterName = parameterName;
            UsedForTypeInference = usedForTypeInference;
            Source = source;
        }

        public void ReplaceSourceWithCapturedVariable(string variableName)
        {
            Source = new TypeInferenceCapturedVariable(variableName);
        }
    }

    protected class TypeInferenceCapturedVariable
    {
        public string VariableName { get; private set; }

        public TypeInferenceCapturedVariable(string variableName)
        {
            VariableName = variableName;
        }
    }
}
