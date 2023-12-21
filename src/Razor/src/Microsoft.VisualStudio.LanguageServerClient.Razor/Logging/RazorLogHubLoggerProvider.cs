// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Editor.Razor.Logging;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;

[Shared]
[Export(typeof(IRazorLoggerProvider))]
internal sealed class RazorLogHubLoggerProvider : IRazorLoggerProvider
{
    // Unique, monotonically increasing ID to identify LogHub session to persist
    // across server restarts.
    private static int s_logHubSessionId;
    private const string LogFileIdentifier = "Razor";

    private readonly RazorLogHubTraceProvider _traceProvider;
    private readonly RazorLogger _razorLogger;
    private readonly Queue<(TraceEventType Level, string Message, object[] Args)> _messageQueue;
    private readonly CancellationTokenSource _disposalTokenSource;
    private TraceSource? _traceSource;

    [ImportingConstructor]
    public RazorLogHubLoggerProvider(RazorLogHubTraceProvider traceProvider, RazorLogger razorLogger)
    {
        _traceProvider = traceProvider ?? throw new ArgumentNullException(nameof(traceProvider));
        _razorLogger = razorLogger ?? throw new ArgumentNullException(nameof(razorLogger));

        _messageQueue = new();
        _disposalTokenSource = new();

        _ = InitializeAsync(_disposalTokenSource.Token);
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        // Ensure that we're never on the UI thread before initialize
        // I suspect this is overkill :D
        await TaskScheduler.Default.SwitchTo(alwaysYield: true);

        var logInstanceNumber = Interlocked.Increment(ref s_logHubSessionId);
        var traceSource = await _traceProvider.InitializeTraceAsync(LogFileIdentifier, logInstanceNumber, cancellationToken).ConfigureAwait(false);
        if (traceSource is null)
        {
            traceSource = new TraceSource("None", SourceLevels.Off) ;
            traceSource.Listeners.Clear();
            _razorLogger.LogError("Could not initialize trace source for Razor LogHub logger. No LogHub log will be created.");
            return;
        }

        _traceSource = traceSource;

        while (_messageQueue.Count > 0)
        {
            var value = _messageQueue.Dequeue();
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            traceSource.TraceEvent(value.Level, id: 0, value.Message, value.Args);
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new RazorLogHubLogger(categoryName, this);
    }

    public void Dispose()
    {
        _disposalTokenSource.Cancel();
    }

    /// <summary>
    /// Tries to log the specified method to LogHub
    /// </summary>
    /// <remarks>
    /// LogHub initialization is <see langword="async"/>, so its possible we'll start to receive log messages
    /// before things are initialized. In that case this logger will queue up the messages to be logged later.
    /// </remarks>
    internal void TryLog(TraceEventType level, string message, params object[] args)
    {
        if (_traceSource is not null)
        {
            _traceSource.TraceEvent(level, id: 0, message, args);
        }
        else
        {
            _messageQueue.Enqueue((level, message, args));
        }
    }
}
