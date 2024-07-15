// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Razor.Logging;

namespace Microsoft.CodeAnalysis.Remote.Razor.Logging;

internal sealed class EmptyLoggerFactory : AbstractLoggerFactory
{
    public static ILoggerFactory Instance { get; } = new EmptyLoggerFactory();

    private EmptyLoggerFactory()
        : base(ImmutableArray<ILoggerProvider>.Empty)
    {
    }
}
