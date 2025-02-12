// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.AspNetCore.Razor.Language;

[Flags]
public enum RazorParserOptionsFlags
{
    DesignTime = 1 << 0,
    ParseLeadingDirectives = 1 << 1,
    UseRoslynTokenizer = 1 << 2,
    EnableSpanEditHandlers = 1 << 3,
}
