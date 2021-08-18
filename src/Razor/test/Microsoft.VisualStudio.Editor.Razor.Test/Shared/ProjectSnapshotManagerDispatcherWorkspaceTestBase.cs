// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Threading;

namespace Xunit
{
    public abstract class ProjectSnapshotManagerDispatcherWorkspaceTestBase : WorkspaceTestBase
    {
        internal ProjectSnapshotManagerDispatcher Dispatcher { get; } = new TestProjectSnapshotManagerDispatcher();

        internal SolutionCloseTracker SolutionCloseTracker { get; } = new TestSolutionCloseTracker();

        internal static JoinableTaskFactory JoinableTaskFactory
        {
            get
            {
                var joinableTaskContext = new JoinableTaskContextNode(new JoinableTaskContext());
                return new JoinableTaskFactory(joinableTaskContext.Context);
            }
        }
    }
}
