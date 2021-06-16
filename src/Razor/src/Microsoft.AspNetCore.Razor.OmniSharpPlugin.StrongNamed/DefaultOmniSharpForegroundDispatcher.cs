// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.OmniSharpPlugin
{
    public class DefaultOmniSharpForegroundDispatcher : OmniSharpForegroundDispatcher
    {
        public DefaultOmniSharpForegroundDispatcher()
        {
            InternalDispatcher = new DefaultForegroundDispatcher();
        }

        public override bool IsForegroundThread => InternalDispatcher.IsSpecializedForegroundThread;
        public override TaskScheduler ForegroundScheduler => InternalDispatcher.SpecializedForegroundScheduler;
        public override TaskScheduler BackgroundScheduler => InternalDispatcher.BackgroundScheduler;

        public override void AssertBackgroundThread([CallerMemberName] string caller = null) => InternalDispatcher.AssertBackgroundThread(caller);
        public override void AssertForegroundThread([CallerMemberName] string caller = null) => InternalDispatcher.AssertSpecializedForegroundThread(caller);
    }
}
