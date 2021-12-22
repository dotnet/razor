// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Razor.Integration.Test.InProcess
{
    internal class TestServices
    {
        protected TestServices(JoinableTaskFactory joinableTaskFactory)
        {
            JoinableTaskFactory = joinableTaskFactory;

            Editor = new EditorInProcess(this);
            SolutionExplorer = new SolutionExplorerInProcess(this);
            Workspace = new WorkspaceInProcess(this);
            ErrorList = new ErrorListInProcess(this);
        }

        public JoinableTaskFactory JoinableTaskFactory { get; }

        public EditorInProcess Editor { get; }

        public ErrorListInProcess ErrorList { get; }

        public SolutionExplorerInProcess SolutionExplorer { get; }

        public WorkspaceInProcess Workspace { get; }

        internal static async Task<TestServices> CreateAsync(JoinableTaskFactory joinableTaskFactory)
        {
            var services = new TestServices(joinableTaskFactory);
            await services.InitializeAsync();
            return services;
        }

        protected virtual Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        internal T InvokeOnUIThread<T>(Func<CancellationToken, T> action, CancellationToken cancellationToken)
        {
            var operation = JoinableTaskFactory.RunAsync(async () =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                return action(cancellationToken);
            });

            operation.Task.Wait(cancellationToken);
            return operation.Task.Result;
        }
    }
}
