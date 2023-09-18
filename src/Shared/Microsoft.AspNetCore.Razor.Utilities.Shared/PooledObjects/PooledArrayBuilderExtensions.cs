// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.CompilerServices;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal static class PooledArrayBuilderExtensions
{
    public static ref PooledArrayBuilder<T> AsRef<T>(this in PooledArrayBuilder<T> builder)
        => ref Unsafe.AsRef(in builder);
}
