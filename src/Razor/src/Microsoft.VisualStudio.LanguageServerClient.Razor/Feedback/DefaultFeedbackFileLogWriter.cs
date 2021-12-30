﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Feedback
{
    internal class DefaultFeedbackFileLogWriter : FeedbackFileLogWriter, IDisposable
    {
        private readonly ConcurrentQueue<string> _logs;
        private readonly SemaphoreSlim _logSemaphore;
        private readonly Task _logWriterTask;
        private readonly object _writeToLock;
        private readonly FeedbackLogDirectoryProvider _feedbackLogDirectoryProvider;
        private TextWriter _logWriter;
        private bool _disposed;

        public DefaultFeedbackFileLogWriter(FeedbackLogDirectoryProvider feedbackLogDirectoryProvider, string logFileIdentifier)
        {
            if (feedbackLogDirectoryProvider is null)
            {
                throw new ArgumentNullException(nameof(feedbackLogDirectoryProvider));
            }

            _logs = new ConcurrentQueue<string>();
            _logSemaphore = new SemaphoreSlim(0);
            _writeToLock = new object();
            _feedbackLogDirectoryProvider = feedbackLogDirectoryProvider;

            _logWriter = InitializeLogFile(logFileIdentifier);

            _logWriterTask = Task.Run(WriteToLogAsync);
        }

        public override void Write(string message)
        {
            lock (_writeToLock)
            {
                if (_disposed)
                {
                    return;
                }

                var timeStampString = DateTimeOffset.UtcNow.ToString("mm:ss.fff", CultureInfo.InvariantCulture);
                var log = $"{timeStampString} | {message}";
                _logs.Enqueue(log);
                _logSemaphore.Release();
            }
        }

        public void Dispose()
        {
            lock (_writeToLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            // Releasing the semaphore here causes our WriteToLogAsync loop to exit.
            _logSemaphore.Release();
            _logSemaphore.Dispose();

#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            _logWriterTask.Wait();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits

            _logWriter.Close();
            _logWriter.Dispose();
        }

        private async Task WriteToLogAsync()
        {
            while (true)
            {
                await _logSemaphore.WaitAsync();
                if (!_logs.TryDequeue(out var line))
                {
                    // An empty queue is a stop signal.
                    break;
                }

                await _logWriter.WriteLineAsync(line);
                await _logWriter.FlushAsync();
            }
        }

        private TextWriter InitializeLogFile(string logFileIdentifier)
        {
            var logDirectory = _feedbackLogDirectoryProvider.GetDirectory();

            // Ensure a unique file name, in case another log session started around the same time.
            for (var index = 0; ; index++)
            {
                var fileName = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}_{1:yyyyMMdd_HHmmss}{2}.log",
                    logFileIdentifier,
                    DateTime.UtcNow,
                    index == 0 ? string.Empty : "." + index);

                var filePath = Path.Combine(logDirectory, fileName);
                try
                {
                    var fileStream = File.Open(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);

                    return new StreamWriter(fileStream);
                }
                catch (IOException)
                {
                    // The file probably already exists. Try again with the next index,
                    // unless there were already too many failures.
                    if (index == 9) throw;
                }
            }
        }
    }
}
