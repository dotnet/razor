// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal partial class StringBuilderPool
{
    private sealed class Policy : PooledObjectPolicy
    {
        public static readonly Policy Default = new();

        private Policy()
        {
        }

        public override StringBuilder Create() => new();

        public override bool Return(StringBuilder builder)
        {
            builder.Clear();

            if (builder.Capacity > DefaultPool.MaximumObjectSize)
            {
                builder.Capacity = DefaultPool.MaximumObjectSize;
            }

            return true;
        }
    }
}
