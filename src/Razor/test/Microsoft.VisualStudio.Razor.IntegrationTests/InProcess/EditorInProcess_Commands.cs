// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Razor.IntegrationTests.InProcess;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Xunit;

namespace Microsoft.VisualStudio.Extensibility.Testing
{
    internal partial class EditorInProcess
    {
        public async Task InvokeGoToDefinitionAsync(CancellationToken cancellationToken)
        {
            await ExecuteCommandAsync(WellKnownCommandNames.Edit_GoToDefinition, cancellationToken);
        }

        public async Task CloseDocumentWindowAsync(CancellationToken cancellationToken)
        {
            await ExecuteCommandAsync(WellKnownCommandNames.Window_CloseDocumentWindow, cancellationToken);
        }

        private async Task ExecuteCommandAsync(string commandName, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var dte = await GetRequiredGlobalServiceAsync<SDTE, EnvDTE.DTE>(cancellationToken);

            Assert.True(dte.Commands.Item(commandName).IsAvailable);

            dte.ExecuteCommand(commandName);
        }
    }
}
