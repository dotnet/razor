// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.AspNetCore.Razor.Language.Components;

namespace Microsoft.AspNetCore.Razor.Language;

public static class FileKinds
{
    /// <summary>
    ///  Returns <see langword="true"/> if the specified value represents a component or component import.
    /// </summary>
    public static bool IsComponent(this RazorFileKind fileKind)
        => fileKind is RazorFileKind.Component or RazorFileKind.ComponentImport;

    /// <summary>
    ///  Returns <see langword="true"/> if the specified value represents a component import.
    /// </summary>
    public static bool IsComponentImport(this RazorFileKind fileKind)
        => fileKind == RazorFileKind.ComponentImport;

    /// <summary>
    ///  Returns <see langword="true"/> if the specified value represents a legacy file kind.
    /// </summary>
    internal static bool IsLegacy(this RazorFileKind fileKind)
        => fileKind == RazorFileKind.Legacy;

    public static RazorFileKind ComponentFilePathToRazorFileKind(string filePath)
    {
        ArgHelper.ThrowIfNull(filePath);

        if (string.Equals(ComponentMetadata.ImportsFileName, Path.GetFileName(filePath), StringComparison.Ordinal))
        {
            return RazorFileKind.ComponentImport;
        }
        else
        {
            return RazorFileKind.Component;
        }
    }

    public static RazorFileKind FilePathToRazorFileKind(string filePath)
    {
        ArgHelper.ThrowIfNull(filePath);

        if (string.Equals(ComponentMetadata.ImportsFileName, Path.GetFileName(filePath), StringComparison.Ordinal))
        {
            return RazorFileKind.ComponentImport;
        }
        else if (string.Equals(".razor", Path.GetExtension(filePath), StringComparison.OrdinalIgnoreCase))
        {
            return RazorFileKind.Component;
        }
        else
        {
            return RazorFileKind.Legacy;
        }
    }
}
