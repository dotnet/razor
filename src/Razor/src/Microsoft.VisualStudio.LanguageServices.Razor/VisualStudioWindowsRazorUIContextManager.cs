// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Threading;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.LanguageServices.Razor
{
    [Export(typeof(RazorUIContextManager))]
    internal class VisualStudioWindowsRazorUIContextManager : RazorUIContextManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly JoinableTaskFactory _joinableTaskFactory;

        [ImportingConstructor]
        public VisualStudioWindowsRazorUIContextManager(SVsServiceProvider serviceProvider!!, JoinableTaskContext joinableTaskContext!!)
        {
            _serviceProvider = serviceProvider;
            _joinableTaskFactory = joinableTaskContext.Factory;
        }

        public override async Task SetUIContextAsync(Guid uiContextGuid, bool isActive, CancellationToken cancellationToken)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var monitorSelection = _serviceProvider.GetService(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;
            Assumes.Present(monitorSelection);
            var cookieResult = monitorSelection.GetCmdUIContextCookie(uiContextGuid, out var cookie);
            ErrorHandler.ThrowOnFailure(cookieResult);

            var setContextResult = monitorSelection.SetCmdUIContext(cookie, isActive ? 1 : 0);
            ErrorHandler.ThrowOnFailure(setContextResult);
        }
    }
}
