// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;

namespace Microsoft.CodeAnalysis.Razor;

internal static class ProjectSnapshotManagerDispatcherExtensions
{
    public static void AssertDispatcherThread(this ProjectSnapshotManagerDispatcher dispatcher, [CallerMemberName] string? caller = null)
    {
        if (!dispatcher.IsDispatcherThread)
        {
            caller = caller is null ? "The method" : $"'{caller}'";
            throw new InvalidOperationException(caller + " must be called on the project snapshot manager's thread.");
        }
    }
}
