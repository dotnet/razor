// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Razor
{
    internal abstract class ForegroundDispatcher
    {
        public abstract bool IsForegroundThread { get; }

        public abstract bool IsBackgroundThread { get; }

        /// <summary>
        /// Single-threaded scheduler intended to be a "fake" UI thread for code that requires
        /// running on a single thread.
        /// </summary>
        public abstract TaskScheduler ForegroundScheduler { get; }

        public abstract TaskScheduler BackgroundScheduler { get; }

        public virtual void AssertForegroundThread([CallerMemberName] string caller = null)
        {
            if (!IsForegroundThread)
            {
                caller = caller == null ? Workspaces.Resources.ForegroundDispatcher_NoMethodNamePlaceholder : $"'{caller}'";
                throw new InvalidOperationException(Workspaces.Resources.FormatForegroundDispatcher_AssertSpecializedForegroundThread(caller));
            }
        }

        // Depending on where it's called from (e.g. language server or editor), a method may be
        // run on the UI thread or the specialized foreground thread.
        public virtual void AssertSpecializedForegroundOrUIThread([CallerMemberName] string caller = null)
        {
            if (IsBackgroundThread)
            {
                caller = caller == null ? Workspaces.Resources.ForegroundDispatcher_NoMethodNamePlaceholder : $"'{caller}'";
                throw new InvalidOperationException(Workspaces.Resources.FormatForegroundDispatcher_AssertSpecializedForegroundOrUIThread(caller));
            }
        }

        public virtual void AssertBackgroundThread([CallerMemberName] string caller = null)
        {
            if (!IsBackgroundThread)
            {
                caller = caller == null ? Workspaces.Resources.ForegroundDispatcher_NoMethodNamePlaceholder : $"'{caller}'";
                throw new InvalidOperationException(Workspaces.Resources.FormatForegroundDispatcher_AssertBackgroundThread(caller));
            }
        }
    }
}
