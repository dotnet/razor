// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    [Shared]
    [Export(typeof(RazorLogger))]
    internal class ActivityLogRazorLogger : RazorLogger
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly JoinableTaskFactory _joinableTaskFactory;

        [ImportingConstructor]
        public ActivityLogRazorLogger(SVsServiceProvider serviceProvider, JoinableTaskContext joinableTaskContext)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (joinableTaskContext is null)
            {
                throw new ArgumentNullException(nameof(joinableTaskContext));
            }

            _serviceProvider = serviceProvider;
            _joinableTaskFactory = joinableTaskContext.Factory;
        }

        public override void LogError(string message)
        {
            Log(__ACTIVITYLOG_ENTRYTYPE.ALE_ERROR, message);
        }

        public override void LogWarning(string message)
        {
            Log(__ACTIVITYLOG_ENTRYTYPE.ALE_WARNING, message);
        }

        public override void LogVerbose(string message)
        {
            Log(__ACTIVITYLOG_ENTRYTYPE.ALE_INFORMATION, message);
        }

        public override void SetUIContext(Guid uiContextGuid, bool isActive)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var monitorSelection = _serviceProvider.GetService(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;
            Assumes.Present(monitorSelection);
            var cookieResult = monitorSelection.GetCmdUIContextCookie(uiContextGuid, out var cookie);
            Assumes.True(cookieResult == VSConstants.S_OK, $"GetCmdUIContextCookie failed with error code {cookieResult}.");

            var setContextResult = monitorSelection.SetCmdUIContext(cookie, isActive ? 1 : 0);
            Assumes.True(setContextResult == VSConstants.S_OK, $"SetCmdUIContext faied with error code {setContextResult}.");
        }

        private async void Log(__ACTIVITYLOG_ENTRYTYPE logType, string message)
        {
            // This is an async void method. Catch all exceptions so it doesn't crash the process.
            try
            {
                await _joinableTaskFactory.SwitchToMainThreadAsync();

                var activityLog = GetActivityLog();
                if (activityLog != null)
                {
                    var hr = activityLog.LogEntry(
                        (uint)logType,
                        "Razor LSP Client",
                        $"Info:{Environment.NewLine}{message}");
                    ErrorHandler.ThrowOnFailure(hr);
                }
            }
            catch (Exception ex)
            {
                Debug.Fail($"Razor LSP client logging failed. Error: {ex.Message}");
            }
        }

        private IVsActivityLog GetActivityLog()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return _serviceProvider.GetService(typeof(SVsActivityLog)) as IVsActivityLog;
        }
    }
}
