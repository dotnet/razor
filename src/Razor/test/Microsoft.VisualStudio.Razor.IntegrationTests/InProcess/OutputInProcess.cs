// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Razor.IntegrationTests.Extensions;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.VisualStudio.Extensibility.Testing
{
    [TestService]
    internal partial class OutputInProcess
    {
        private const string RazorPaneName = "Razor Language Server Client";

        public async Task<string> GetOutputContentAsync(CancellationToken cancellationToken)
        {
            var outputPaneTextView = GetOutputPaneTextView(RazorPaneName);
            return await outputPaneTextView.GetContentAsync(JoinableTaskFactory, cancellationToken);
        }

        private static IVsTextView GetOutputPaneTextView(string paneName)
        {
            var sVSOutputWindow = ServiceProvider.GlobalProvider.GetService<SVsOutputWindow, IVsOutputWindow>();
            if (sVSOutputWindow is not IVsExtensibleObject extensibleObject)
            {
                throw new InvalidOperationException();
            }

            extensibleObject.GetAutomationObject(null, out var outputWindowObj);
            var outputWindow = (EnvDTE.OutputWindow)outputWindowObj;

            var pane = outputWindow.OutputWindowPanes.Item(paneName);

            var guid = Guid.Parse(pane.Guid);
            sVSOutputWindow.GetPane(guid, out var result);

            if (result is not IVsTextView textView)
            {
                throw new NotImplementedException();
            }

            return textView;
        }

    }
}
