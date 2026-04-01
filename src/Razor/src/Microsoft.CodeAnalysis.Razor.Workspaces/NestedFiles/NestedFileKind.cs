// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.CodeAnalysis.Razor.NestedFiles;

/// <summary>
/// Specifies which type of nested file to create for a Razor component.
/// </summary>
internal static class NestedFileKind
{
    public const string Css = "css";
    public const string CSharp = "csharp";
    public const string JavaScript = "javascript";
}
