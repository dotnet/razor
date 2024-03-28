﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

internal class LiteralRuntimeNodeWriter : RuntimeNodeWriter
{
    public override string WriteCSharpExpressionMethod { get; set; } = "WriteLiteral";
}
