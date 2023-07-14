// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.Utilities;

internal abstract class ChecksumWithChildren(ImmutableArray<object> children) : IChecksummedObject
{
    public Checksum Checksum { get; } = Checksum.Create(children.SelectAsArray(c => c as Checksum ?? ((ChecksumCollection)c).Checksum));
    public ImmutableArray<object> Children { get; } = children;
}
