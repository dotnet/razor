﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language;

public interface IRazorOptimizationPass : IRazorEngineFeature
{
    int Order { get; }

    void Execute(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode);
}
