// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.OmniSharp.Project;

internal class OmniSharpProjectSnapshotManagerDispatcher
{
    internal OmniSharpProjectSnapshotManagerDispatcher()
    {
        InternalDispatcher = new InternalOmniSharpProjectSnapshotManagerDispatcher();
    }

    internal ProjectSnapshotManagerDispatcher InternalDispatcher { get; private protected set; }

    internal Task RunOnDispatcherThreadAsync(Action action, CancellationToken cancellationToken)
        => InternalDispatcher.RunOnDispatcherThreadAsync(action, cancellationToken);

    internal Task<TResult> RunOnDispatcherThreadAsync<TResult>(Func<TResult> action, CancellationToken cancellationToken)
        => InternalDispatcher.RunOnDispatcherThreadAsync(action, cancellationToken);
    internal TaskScheduler DispatcherScheduler => InternalDispatcher.DispatcherScheduler;

    internal void AssertDispatcherThread([CallerMemberName] string caller = null) => InternalDispatcher.AssertDispatcherThread(caller);

    private class InternalOmniSharpProjectSnapshotManagerDispatcher : ProjectSnapshotManagerDispatcherBase
    {
        private const string ThreadName = "Razor." + nameof(OmniSharpProjectSnapshotManagerDispatcher);

        internal InternalOmniSharpProjectSnapshotManagerDispatcher() : base(ThreadName)
        {
        }

        public override void LogException(Exception ex)
        {
            // We don't currently have logging mechanisms in place for O#.
        }
    }
}
