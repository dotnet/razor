// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

internal class LiteralRuntimeNodeWriter(CodeRenderingContext context) : RuntimeNodeWriter(context)
{
    public override string WriteCSharpExpressionMethod { get; set; } = "WriteLiteral";
}
