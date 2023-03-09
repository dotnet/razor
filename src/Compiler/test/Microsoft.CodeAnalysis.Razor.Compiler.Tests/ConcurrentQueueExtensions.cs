// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#if !NETCOREAPP

using System.Collections.Concurrent;

namespace Microsoft.CodeAnalysis;

internal static class ConcurrentQueueExtensions
{
    public static void Clear<T>(this ConcurrentQueue<T> queue)
    {
        while (queue.TryDequeue(out _))
            ;
    }
}

#endif
