// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;

namespace Microsoft.CodeAnalysis.Razor;

internal static class ProjectSnapshotManagerDispatcherExtensions
{
    public static DispatcherAwaitable SwitchToAsync(this ProjectSnapshotManagerDispatcher dispatcher, CancellationToken cancellationToken)
    {
        return new DispatcherAwaitable(dispatcher, cancellationToken);
    }
}
