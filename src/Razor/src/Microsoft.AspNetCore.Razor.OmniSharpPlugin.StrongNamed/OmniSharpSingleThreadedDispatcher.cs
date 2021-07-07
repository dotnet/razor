// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.AspNetCore.Razor.OmniSharpPlugin
{
    public abstract class OmniSharpSingleThreadedDispatcher
    {
        internal SingleThreadedDispatcher InternalDispatcher { get; private protected set; }

        public abstract bool IsDispatcherThread { get; }

        public abstract TaskScheduler DispatcherScheduler { get; }

        public abstract void AssertDispatcherThread([CallerMemberName] string caller = null);
    }
}
