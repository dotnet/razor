// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Microsoft.VisualStudio.Razor.Integration.Test.InProcess
{
    internal partial class EditorInProcess
    {
        public async Task DismissCompletionSessionsAsync(CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            //await WaitForCompletionSetAsync(cancellationToken);

            var view = await GetActiveTextViewAsync(cancellationToken);

            var broker = await GetComponentModelServiceAsync<ICompletionBroker>(cancellationToken);
            broker.DismissAllSessions(view);
        }
    }
}
