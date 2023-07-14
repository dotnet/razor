// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.AspNetCore.Razor.Utilities;

internal class ChecksumCollection(ImmutableArray<object> checksums) : ChecksumWithChildren(checksums), IReadOnlyCollection<Checksum>
{
    public ChecksumCollection(ImmutableArray<Checksum> checksums)
        : this(checksums.CastArray<object>())
    {
    }

    public int Count => Children.Length;
    public Checksum this[int index] => (Checksum)Children[index];

    public IEnumerator<Checksum> GetEnumerator()
        => Children.Cast<Checksum>().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}
