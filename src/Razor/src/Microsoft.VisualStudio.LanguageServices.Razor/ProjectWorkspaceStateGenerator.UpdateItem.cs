// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Razor;

internal sealed partial class ProjectWorkspaceStateGenerator
{
    // Internal for testing
    internal class UpdateItem : IDisposable
    {
        public Task UpdateTask { get; }

        private readonly CancellationTokenSource _tokenSource;

        public bool IsCancellationRequested => _tokenSource.IsCancellationRequested;

        public UpdateItem(Func<CancellationToken, Task> updater, CancellationToken token)
        {
            _tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);

            UpdateTask = Task.Run(
                () => updater(_tokenSource.Token),
                _tokenSource.Token);
        }

        public void Dispose()
        {
            _tokenSource.Cancel();
            _tokenSource.Dispose();
        }
    }
}
