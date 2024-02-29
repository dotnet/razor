﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;

namespace Microsoft.AspNetCore.Razor.Language;

[Obsolete("In Razor 2.1 and newer, use RazorCodeDocument.GetParserOptions().")]
public interface IRazorParserOptionsFeature : IRazorEngineFeature
{
    RazorParserOptions GetOptions();
}
