// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language;

public enum RazorFileKind : byte
{
    None = 0,
    Component = 1,
    ComponentImport = 2,
    Legacy = 3
}
