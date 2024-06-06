// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Razor.LanguageClient.ProjectSystem;

internal sealed partial class RazorProjectInfoDriver
{
    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(RazorProjectInfoDriver instance)
    {
        public async Task WaitForInitializeAsync()
        {
            await instance._initializeTask;
        }

        public Task WaitUntilCurrentBatchCompletesAsync()
            => instance._workQueue.WaitUntilCurrentBatchCompletesAsync();
    }
}
