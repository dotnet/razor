// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.Razor.Integration.Test.InProcess
{
    internal class ErrorListInProcess : InProcComponent
    {
        public ErrorListInProcess(TestServices testServices)
            : base(testServices)
        {
        }

        public async Task<IEnumerable<IVsTaskItem>> GetErrorsAsync(CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var errorListItems = VsShellUtilities.GetErrorListItems(ServiceProvider.GlobalProvider);
            return errorListItems;
        }
    }
}
