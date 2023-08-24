// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Editor.Razor.Logging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.LanguageServices.Razor.Logging;

[Shared]
[Export(typeof(IOutputWindowLogger))]
internal class OutputWindowLogger : IOutputWindowLogger
{
#if DEBUG
    private const LogLevel MinimumLogLevel = LogLevel.Debug;
#else
    private const LogLevel MinimumLogLevel = LogLevel.Warning;
#endif

    private readonly OutputPane _outputPane;

    [ImportingConstructor]
    public OutputWindowLogger(JoinableTaskContext joinableTaskContext)
    {
        _outputPane = new OutputPane(joinableTaskContext);
    }

    [Obsolete("Exists only for Mock.")]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    internal OutputWindowLogger()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {
    }

    public IDisposable BeginScope<TState>(TState state) => Scope.Instance;

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= MinimumLogLevel;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            _outputPane.WriteLine(DateTime.Now.ToString("h:mm:ss.fff ") + formatter(state, exception));
            if (exception is not null)
            {
                _outputPane.WriteLine(exception.ToString());
            }
        }
    }

    private class OutputPane
    {
        private static readonly Guid s_outputPaneGuid = new("BBAFF416-4AF5-41F2-9F93-91F283E43C3B");

        private readonly JoinableTaskContext _threadingContext;
        private readonly IServiceProvider _serviceProvider;
        private IVsOutputWindowPane? _doNotAccessDirectlyOutputPane;
        private AsyncQueue<string> _outputQueue = new();

        public OutputPane(JoinableTaskContext threadingContext)
        {
            _threadingContext = threadingContext;
            _serviceProvider = ServiceProvider.GlobalProvider;
        }

        public void WriteLine(string value)
        {
            _outputQueue.Enqueue(value);

            _ = DequeueAsync();
        }

        private async Task DequeueAsync()
        {
            var value = await _outputQueue.DequeueAsync();
            if (value is null)
            {
                return;
            }

            var pane = await GetPaneAsync();
            if (pane is null)
            {
                return;
            }

            // https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.interop.ivsoutputwindowpane.outputstringthreadsafe?view=visualstudiosdk-2022#remarks
            if (pane is IVsOutputWindowPaneNoPump noPumpPane)
            {
                noPumpPane.OutputStringNoPump(value + Environment.NewLine);
            }
            else
            {
                pane.OutputStringThreadSafe(value + Environment.NewLine);
            }
        }

        private async Task<IVsOutputWindowPane> GetPaneAsync()
        {
            if (_doNotAccessDirectlyOutputPane is null)
            {
                await _threadingContext.Factory.SwitchToMainThreadAsync();

                if (_doNotAccessDirectlyOutputPane != null)
                {
                    // check whether other one already initialized output window.
                    // the output API already handle double initialization, so this is just quick bail
                    // rather than any functional issue
                    return _doNotAccessDirectlyOutputPane;
                }

                var outputWindow = (IVsOutputWindow)_serviceProvider.GetService(typeof(SVsOutputWindow));

                // this should bring outout window to the front
                _doNotAccessDirectlyOutputPane = CreateOutputPane(outputWindow);
            }

            return _doNotAccessDirectlyOutputPane!;
        }

        private IVsOutputWindowPane? CreateOutputPane(IVsOutputWindow outputWindow)
        {
            _threadingContext.AssertUIThread();

            // Try to get the workspace pane if it has already been registered
            var workspacePaneGuid = s_outputPaneGuid;

            // If the pane has already been created, CreatePane returns it
            if (ErrorHandler.Succeeded(outputWindow.CreatePane(ref workspacePaneGuid, "Razor Logger Output", fInitVisible: 1, fClearWithSolution: 1)) &&
                ErrorHandler.Succeeded(outputWindow.GetPane(ref workspacePaneGuid, out var pane)))
            {
                return pane;
            }

            return null;
        }
    }

    private class Scope : IDisposable
    {
        public static readonly Scope Instance = new();

        public void Dispose()
        {
        }
    }
}
