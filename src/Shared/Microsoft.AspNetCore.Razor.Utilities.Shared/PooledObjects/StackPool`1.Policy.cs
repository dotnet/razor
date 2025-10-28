// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal partial class StackPool<T>
{
    private sealed class Policy : PooledObjectPolicy
    {
        public static readonly Policy Default = new();

        private Policy()
        {
        }

        public override Stack<T> Create() => new();

        public override bool Return(Stack<T> stack)
        {
            var count = stack.Count;

            stack.Clear();

            if (count > DefaultPool.MaximumObjectSize)
            {
                stack.TrimExcess();
            }

            return true;
        }
    }
}
