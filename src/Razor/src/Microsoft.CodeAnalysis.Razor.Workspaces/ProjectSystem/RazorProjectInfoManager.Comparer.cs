// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal abstract partial class RazorProjectInfoPublisher
{
    private sealed class Comparer : IEqualityComparer<RazorProjectInfo>
    {
        public static readonly Comparer Instance = new();

        private Comparer()
        {
        }

        public bool Equals(RazorProjectInfo? x, RazorProjectInfo? y)
        {
            if (x is null)
            {
                return y is null;
            }
            else if (y is null)
            {
                return false;
            }

            return x.ProjectKey.Equals(y.ProjectKey);
        }

        public int GetHashCode(RazorProjectInfo obj)
        {
            return obj.ProjectKey.GetHashCode();
        }
    }
}
