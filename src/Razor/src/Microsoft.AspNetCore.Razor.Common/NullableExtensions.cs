// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime;

namespace Microsoft.AspNetCore.Razor;

internal static class NullableExtensions
{
    [DebuggerStepThrough]
    [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
    public static T AssumeNotNull<T>([NotNull] this T? obj)
        where T : class
        => obj ?? throw new InvalidOperationException();

    [DebuggerStepThrough]
    [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
    public static T AssumeNotNull<T>([NotNull] this T? obj)
        where T : struct
        => obj ?? throw new InvalidOperationException();
}
