// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.IO;
using Microsoft.AspNetCore.Razor.Language.Components;

namespace Microsoft.AspNetCore.Razor.Language;

public static class FileKinds
{
    public static readonly string Component = "component";

    public static readonly string ComponentImport = "componentImport";

    public static readonly string Legacy = "mvc";

    public static bool IsComponent(string fileKind)
    {
        // fileKind might be null.
        return string.Equals(fileKind, FileKinds.Component, StringComparison.OrdinalIgnoreCase) || IsComponentImport(fileKind);
    }

    public static bool IsComponentImport(string fileKind)
    {
        // fileKind might be null.
        return string.Equals(fileKind, FileKinds.ComponentImport, StringComparison.OrdinalIgnoreCase);
    }

#nullable enable
    internal static bool IsLegacy(string? fileKind)
    {
        return string.Equals(fileKind, FileKinds.Legacy, StringComparison.OrdinalIgnoreCase);
    }
#nullable disable

    public static string GetComponentFileKindFromFilePath(string filePath)
    {
        if (filePath == null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        if (string.Equals(ComponentMetadata.ImportsFileName, Path.GetFileName(filePath), StringComparison.Ordinal))
        {
            return FileKinds.ComponentImport;
        }
        else
        {
            return FileKinds.Component;
        }
    }

    public static string GetFileKindFromFilePath(string filePath)
    {
        if (filePath == null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        if (string.Equals(ComponentMetadata.ImportsFileName, Path.GetFileName(filePath), StringComparison.Ordinal))
        {
            return FileKinds.ComponentImport;
        }
        else if (string.Equals(".razor", Path.GetExtension(filePath), StringComparison.OrdinalIgnoreCase))
        {
            return FileKinds.Component;
        }
        else
        {
            return FileKinds.Legacy;
        }
    }

#nullable enable
    public static string FromRazorFileKind(RazorFileKind razorFileKind)
        => razorFileKind switch
        {
            RazorFileKind.Component => Component,
            RazorFileKind.ComponentImport => ComponentImport,
            RazorFileKind.Legacy => Legacy,
            _ => Assumed.Unreachable<string>(),
        };

    public static string? FromRazorFileKind(RazorFileKind? razorFileKind)
        => razorFileKind switch
        {
            RazorFileKind.Component => Component,
            RazorFileKind.ComponentImport => ComponentImport,
            RazorFileKind.Legacy => Legacy,
            null => null,
            _ => Assumed.Unreachable<string>(),
        };

    public static RazorFileKind? ToNullableRazorFileKind(string? fileKind)
        => fileKind is not null
            ? ToRazorFileKind(fileKind)
            : null;

    public static RazorFileKind ToRazorFileKind(string fileKind)
    {
        ArgHelper.ThrowIfNull(fileKind);

        if (IsComponentImport(fileKind))
        {
            return RazorFileKind.ComponentImport;
        }

        if (IsComponent(fileKind))
        {
            return RazorFileKind.Component;
        }

        if (IsLegacy(fileKind))
        {
            return RazorFileKind.Legacy;
        }

        return RazorFileKind.None;
    }

    public static bool IsComponent(RazorFileKind fileKind)
    {
        return fileKind == RazorFileKind.Component || IsComponentImport(fileKind);
    }

    public static bool IsComponentImport(RazorFileKind fileKind)
    {
        return fileKind == RazorFileKind.ComponentImport;
    }

    internal static bool IsLegacy(RazorFileKind fileKind)
    {
        return fileKind == RazorFileKind.Legacy;
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

#nullable disable
}
