// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.OmniSharpPlugin
{
    public class DefaultOmniSharpProjectSnapshotManagerDispatcher : OmniSharpProjectSnapshotManagerDispatcher
    {
        public DefaultOmniSharpProjectSnapshotManagerDispatcher()
        {
            InternalDispatcher = new DefaultProjectSnapshotManagerDispatcher();
        }

        public override TaskScheduler DispatcherScheduler => InternalDispatcher.DispatcherScheduler;

        public override void AssertDispatcherThread([CallerMemberName] string caller = null) => InternalDispatcher.AssertDispatcherThread(caller);
    }
}
