// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.OmniSharpPlugin.StrongNamed
{
    public class OmniSharpWorkspaceProjectStateChangeDetector : IOmniSharpProjectSnapshotManagerChangeTrigger
    {
        public OmniSharpWorkspaceProjectStateChangeDetector(
            OmniSharpSingleThreadedDispatcher singleThreadedDispatcher,
            OmniSharpProjectWorkspaceStateGenerator workspaceStateGenerator)
        {
            if (singleThreadedDispatcher == null)
            {
                throw new ArgumentNullException(nameof(singleThreadedDispatcher));
            }

            if (workspaceStateGenerator == null)
            {
                throw new ArgumentNullException(nameof(workspaceStateGenerator));
            }

            InternalWorkspaceProjectStateChangeDetector = new ForegroundWorkspaceProjectStateChangeDetector(
                singleThreadedDispatcher.InternalDispatcher,
                workspaceStateGenerator.InternalWorkspaceStateGenerator);
        }

        internal WorkspaceProjectStateChangeDetector InternalWorkspaceProjectStateChangeDetector { get; }

        public void Initialize(OmniSharpProjectSnapshotManagerBase projectManager)
        {
            InternalWorkspaceProjectStateChangeDetector.Initialize(projectManager.InternalProjectSnapshotManager);
        }

        private class ForegroundWorkspaceProjectStateChangeDetector : WorkspaceProjectStateChangeDetector
        {
            private readonly SingleThreadedDispatcher _singleThreadedDispatcher;

            public ForegroundWorkspaceProjectStateChangeDetector(
                SingleThreadedDispatcher singleThreadedDispatcher,
                ProjectWorkspaceStateGenerator workspaceStateGenerator) : base(workspaceStateGenerator, singleThreadedDispatcher)
            {
                if (singleThreadedDispatcher is null)
                {
                    throw new ArgumentNullException(nameof(singleThreadedDispatcher));
                }

                _singleThreadedDispatcher = singleThreadedDispatcher;
            }

            // We override the InitializeSolution in order to enforce calls to this to be on the single-threaded dispatcher's
            // thread. OmniSharp currently has an issue where they update the Solution on multiple different threads resulting
            // in change events dispatching through the Workspace on multiple different threads. This normalizes
            // that abnormality.
#pragma warning disable VSTHRD100 // Avoid async void methods
            protected override async void InitializeSolution(Solution solution)
#pragma warning restore VSTHRD100 // Avoid async void methods
            {
                if (_singleThreadedDispatcher.IsDispatcherThread)
                {
                    base.InitializeSolution(solution);
                    return;
                }

                await Task.Factory.StartNew(
                    () =>
                    {
                        try
                        {
                            base.InitializeSolution(solution);
                        }
                        catch (Exception ex)
                        {
                            Debug.Fail("Unexpected error when initializing solution: " + ex);
                        }
                    },
                    CancellationToken.None,
                    TaskCreationOptions.None,
                    _singleThreadedDispatcher.DispatcherScheduler);
            }

            // We override Workspace_WorkspaceChanged in order to enforce calls to this to be on the single-threaded dispatcher's
            // thread. OmniSharp currently has an issue where they update the Solution on multiple different threads resulting
            // in change events dispatching through the Workspace on multiple different threads. This normalizes
            // that abnormality.
#pragma warning disable VSTHRD100 // Avoid async void methods
            internal override async void Workspace_WorkspaceChanged(object sender, WorkspaceChangeEventArgs args)
#pragma warning restore VSTHRD100 // Avoid async void methods
            {
                if (_singleThreadedDispatcher.IsDispatcherThread)
                {
                    base.Workspace_WorkspaceChanged(sender, args);
                    return;
                }
                await Task.Factory.StartNew(
                    () =>
                    {
                        try
                        {
                            base.Workspace_WorkspaceChanged(sender, args);
                        }
                        catch (Exception ex)
                        {
                            Debug.Fail("Unexpected error when handling a workspace changed event: " + ex);
                        }
                    },
                    CancellationToken.None,
                    TaskCreationOptions.None,
                    _singleThreadedDispatcher.DispatcherScheduler);
            }
        }
    }
}
