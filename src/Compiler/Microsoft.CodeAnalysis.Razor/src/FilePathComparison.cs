// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.CodeAnalysis.Razor;

internal static class FilePathComparison
{
    private static int _instance = -1;

    public static StringComparison Instance
    {
        get
        {
            if (_instance == -1)
            {
                Interlocked.CompareExchange(ref _instance, (int)GetComparison(), -1);
            }

            return (StringComparison)_instance;

            static StringComparison GetComparison()
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? StringComparison.Ordinal
                    : StringComparison.OrdinalIgnoreCase;
            }
        }
    }
}
