﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Protocol;

internal class RazorBreakpointSpanParams
{
    public required Uri Uri { get; init; }

    public required Position Position { get; init; }
}
