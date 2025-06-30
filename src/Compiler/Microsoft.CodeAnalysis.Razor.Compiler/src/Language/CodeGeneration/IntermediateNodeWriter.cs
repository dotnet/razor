// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

public abstract class IntermediateNodeWriter(CodeRenderingContext context)
{
    protected CodeRenderingContext Context { get; } = context;

    public abstract void WriteUsingDirective(UsingDirectiveIntermediateNode node);

    public abstract void WriteCSharpExpression(CSharpExpressionIntermediateNode node);

    public abstract void WriteCSharpCode(CSharpCodeIntermediateNode node);

    public abstract void WriteHtmlContent(HtmlContentIntermediateNode node);

    public abstract void WriteHtmlAttribute(HtmlAttributeIntermediateNode node);

    public abstract void WriteHtmlAttributeValue(HtmlAttributeValueIntermediateNode node);

    public abstract void WriteCSharpExpressionAttributeValue(CSharpExpressionAttributeValueIntermediateNode node);

    public abstract void WriteCSharpCodeAttributeValue(CSharpCodeAttributeValueIntermediateNode node);

    public virtual void WriteComponent(ComponentIntermediateNode node)
        => throw new NotSupportedException("This writer does not support components.");

    public virtual void WriteComponentAttribute(ComponentAttributeIntermediateNode node)
        => throw new NotSupportedException("This writer does not support components.");

    public virtual void WriteComponentChildContent(ComponentChildContentIntermediateNode node)
        => throw new NotSupportedException("This writer does not support components.");

    public virtual void WriteComponentTypeArgument(ComponentTypeArgumentIntermediateNode node)
        => throw new NotSupportedException("This writer does not support components.");

    public virtual void WriteComponentTypeInferenceMethod(ComponentTypeInferenceMethodIntermediateNode node)
        => throw new NotSupportedException("This writer does not support components.");

    public virtual void WriteMarkupElement(MarkupElementIntermediateNode node)
        => throw new NotSupportedException("This writer does not support components.");

    public virtual void WriteMarkupBlock(MarkupBlockIntermediateNode node)
        => throw new NotSupportedException("This writer does not support components.");

    public virtual void WriteReferenceCapture(ReferenceCaptureIntermediateNode node)
        => throw new NotSupportedException("This writer does not support components.");

    public virtual void WriteSetKey(SetKeyIntermediateNode node)
        => throw new NotSupportedException("This writer does not support components.");

    public virtual void WriteSplat(SplatIntermediateNode node)
        => throw new NotSupportedException("This writer does not support components.");

    public virtual void WriteRenderMode(RenderModeIntermediateNode node)
        => throw new NotSupportedException("This writer does not support components.");

    public virtual void WriteFormName(FormNameIntermediateNode node)
        => throw new NotSupportedException("This writer does not support components.");

    public abstract void BeginWriterScope(string writer);

    public abstract void EndWriterScope();
}
