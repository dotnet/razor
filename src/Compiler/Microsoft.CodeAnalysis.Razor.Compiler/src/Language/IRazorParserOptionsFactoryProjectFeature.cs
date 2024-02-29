﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;

namespace Microsoft.AspNetCore.Razor.Language;

internal interface IRazorParserOptionsFactoryProjectFeature : IRazorProjectEngineFeature
{
    RazorParserOptions Create(string fileKind, Action<RazorParserOptionsBuilder> configure);
}
