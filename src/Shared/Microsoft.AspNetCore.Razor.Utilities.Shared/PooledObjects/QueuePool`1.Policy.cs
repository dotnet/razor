// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal partial class QueuePool<T>
{
    private sealed class Policy : PooledObjectPolicy
    {
        public static readonly Policy Default = new();

        private Policy()
        {
        }

        public override Queue<T> Create() => new();

        public override bool Return(Queue<T> queue)
        {
            var count = queue.Count;

            queue.Clear();

            if (count > DefaultPool.MaximumObjectSize)
            {
                queue.TrimExcess();
            }

            return true;
        }
    }
}
