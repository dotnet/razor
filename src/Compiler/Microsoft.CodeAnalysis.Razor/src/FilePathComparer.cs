// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Razor;

namespace Microsoft.CodeAnalysis.Razor;

internal static class FilePathComparer
{
    private static StringComparer? _instance;

    public static StringComparer Instance
    {
        get
        {
            return _instance ?? InterlockedOperations.Initialize(ref _instance, GetComparer());

            static StringComparer GetComparer()
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? StringComparer.Ordinal
                    : StringComparer.OrdinalIgnoreCase;
            }
        }
    }
}
