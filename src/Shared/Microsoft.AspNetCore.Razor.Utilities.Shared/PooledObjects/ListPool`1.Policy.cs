// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal partial class ListPool<T>
{
    private sealed class Policy : PooledObjectPolicy
    {
        public static readonly Policy Default = new();

        private Policy()
        {
        }

        public override List<T> Create() => [];

        public override bool Return(List<T> list)
        {
            var count = list.Count;

            list.Clear();

            if (count > DefaultPool.MaximumObjectSize)
            {
                list.TrimExcess();
            }

            return true;
        }
    }
}
