// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language.Syntax;

internal sealed partial class MarkupTagHelperElementSyntax
{
    private static readonly string TagHelperInfoKey = typeof(TagHelperInfo).Name;

    public TagHelperInfo? TagHelperInfo
    {
        get
        {
            return this.GetAnnotationValue(TagHelperInfoKey) as TagHelperInfo;
        }
    }

    public MarkupTagHelperElementSyntax WithTagHelperInfo(TagHelperInfo info)
    {
        var existingAnnotations = GetAnnotations();

        var newAnnotations = new SyntaxAnnotation[existingAnnotations.Length + 1];
        existingAnnotations.CopyTo(newAnnotations, 0);
        newAnnotations[^1] = new(TagHelperInfoKey, info);

        var newGreen = Green.WithAnnotationsGreen(newAnnotations);

        return (MarkupTagHelperElementSyntax)newGreen.CreateRed(Parent, Position);
    }
}
