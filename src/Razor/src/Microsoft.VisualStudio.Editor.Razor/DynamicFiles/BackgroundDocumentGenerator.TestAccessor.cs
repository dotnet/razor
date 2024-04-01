// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.Razor.DynamicFiles;

internal partial class BackgroundDocumentGenerator
{
    internal TestAccessor GetTestAccessor() => new(this);

    internal sealed class TestAccessor(BackgroundDocumentGenerator instance)
    {
        /// <summary>
        /// Used in unit tests to ensure we can control when background work starts.
        /// </summary>
        public ManualResetEventSlim? BlockBackgroundWorkStart
        {
            get => instance._blockBackgroundWorkStart;
            set => instance._blockBackgroundWorkStart = value;
        }

        /// <summary>
        /// Used in unit tests to ensure we can know when background work finishes.
        /// </summary>
        public ManualResetEventSlim? NotifyBackgroundWorkStarting
        {
            get => instance._notifyBackgroundWorkStarting;
            set => instance._notifyBackgroundWorkStarting = value;
        }

        /// <summary>
        /// Used in unit tests to ensure we can know when background has captured its current workload.
        /// </summary>
        public ManualResetEventSlim? NotifyBackgroundCapturedWorkload
        {
            get => instance._notifyBackgroundCapturedWorkload;
            set => instance._notifyBackgroundCapturedWorkload = value;
        }

        /// <summary>
        /// Used in unit tests to ensure we can control when background work completes.
        /// </summary>
        public ManualResetEventSlim? BlockBackgroundWorkCompleting
        {
            get => instance._blockBackgroundWorkCompleting;
            set => instance._blockBackgroundWorkCompleting = value;
        }

        /// <summary>
        /// Used in unit tests to ensure we can know when background work finishes.
        /// </summary>
        public ManualResetEventSlim? NotifyBackgroundWorkCompleted
        {
            get => instance._notifyBackgroundWorkCompleted;
            set => instance._notifyBackgroundWorkCompleted = value;
        }

        /// <summary>
        /// Used in unit tests to ensure we can know when errors are reported.
        /// </summary>
        public ManualResetEventSlim? NotifyErrorBeingReported
        {
            get => instance._notifyErrorBeingReported;
            set => instance._notifyErrorBeingReported = value;
        }

        public ImmutableDictionary<DocumentKey, (IProjectSnapshot, IDocumentSnapshot)> GetCurrentWork()
        {
            var builder = ImmutableDictionary.CreateBuilder<DocumentKey, (IProjectSnapshot, IDocumentSnapshot)>();

            lock (instance._work)
            {
                foreach (var (key, value) in instance._work)
                {
                    builder.Add(key, value);
                }
            }

            return builder.ToImmutable();
        }

        public bool HasPendingNotifications
        {
            get
            {
                lock (instance._work)
                {
                    return instance._work.Count > 0;
                }
            }
        }

        public bool IsScheduledOrRunning => instance._timer != null;
    }
}
