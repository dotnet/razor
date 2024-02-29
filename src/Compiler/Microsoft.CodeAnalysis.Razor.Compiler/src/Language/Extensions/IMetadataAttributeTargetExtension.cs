﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.Razor.Language.CodeGeneration;

namespace Microsoft.AspNetCore.Razor.Language.Extensions;

internal interface IMetadataAttributeTargetExtension : ICodeTargetExtension
{
    void WriteRazorCompiledItemAttribute(CodeRenderingContext context, RazorCompiledItemAttributeIntermediateNode node);

    void WriteRazorSourceChecksumAttribute(CodeRenderingContext context, RazorSourceChecksumAttributeIntermediateNode node);

    void WriteRazorCompiledItemMetadataAttribute(CodeRenderingContext context, RazorCompiledItemMetadataAttributeIntermediateNode node);
}
