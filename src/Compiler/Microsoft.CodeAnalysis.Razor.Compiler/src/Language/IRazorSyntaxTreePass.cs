﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.AspNetCore.Razor.Language;

// Internal until we flesh out public RazorSyntaxTree API
internal interface IRazorSyntaxTreePass : IRazorEngineFeature
{
    int Order { get; }

    RazorSyntaxTree Execute(RazorCodeDocument codeDocument, RazorSyntaxTree syntaxTree);
}
