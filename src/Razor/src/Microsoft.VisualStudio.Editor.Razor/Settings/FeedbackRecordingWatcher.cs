// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.IO;
using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Diagnostics;

namespace Microsoft.VisualStudio.Editor.Razor.Settings;

internal class FeedbackRecordingWatcher : IDisposable
{
    public event EventHandler<bool>? FeedbackRecordingChanged;
    public bool IsFeedbackBeingRecorded => _isFeedbackBeingRecorded == 1;

    private readonly string _vsFeedbackSemaphoreFullPath;
    private readonly int _vsProcessId;
    private readonly DateTime _vsProcessStartTime;
    private FileSystemWatcher? _vsFeedbackSemaphoreFileWatcher;
    private int _isFeedbackBeingRecorded;

    private const string VSFeedbackSemaphoreDir = @"Microsoft\VSFeedbackCollector";
    private const string VSFeedbackSemaphoreFileName = "feedback.recording.json";

    public FeedbackRecordingWatcher()
    {
        // Set up logic to determine if a user is recording feedback.
        // This is done by a few things:
        // 1. VS uses a semaphore file on start/stop of recording. Make a file watcher for that file
        // 2. Since multiple VS instances could be running, we want to make sure we're collecting only for the correct one. The file has the PID that we can compare against
        // 3. Use the process start time as a simple check to avoid reading files that were created before this instance of VS was running
        var vsProcess = Process.GetCurrentProcess();
        _vsProcessId = vsProcess.Id;
        _vsProcessStartTime = vsProcess.StartTime;

        var tempDir = Path.GetTempPath();
        var vsFeedbackTempDir = Path.Combine(tempDir, VSFeedbackSemaphoreDir);
        _vsFeedbackSemaphoreFullPath = Path.Combine(vsFeedbackTempDir, VSFeedbackSemaphoreFileName);

        // Directory may not exist in scenarios such as Razor integration tests
        if (!Directory.Exists(vsFeedbackTempDir))
        {
            return;
        }

        _vsFeedbackSemaphoreFileWatcher = new FileSystemWatcher(vsFeedbackTempDir, VSFeedbackSemaphoreFileName);
        _vsFeedbackSemaphoreFileWatcher.Created += (_, _) => OnFeedbackSemaphoreCreatedOrChanged();
        _vsFeedbackSemaphoreFileWatcher.Changed += (_, _) => OnFeedbackSemaphoreCreatedOrChanged();
        _vsFeedbackSemaphoreFileWatcher.Deleted += (_, _) => OnFeedbackSemaphoreDeleted();

        // If the file exists on setup, check to see if it's actually the correct. It's possible a user started feedback before
        // the razor package was loaded.
        if (File.Exists(_vsFeedbackSemaphoreFullPath))
        {
            OnFeedbackSemaphoreCreatedOrChanged();
        }

        _vsFeedbackSemaphoreFileWatcher.EnableRaisingEvents = true;
    }

    public void Dispose()
    {
        (var fileWatcher, _vsFeedbackSemaphoreFileWatcher) = (_vsFeedbackSemaphoreFileWatcher, null);
        fileWatcher?.Dispose();
    }

    private void OnFeedbackSemaphoreDeleted()
    {
        Interlocked.Exchange(ref _isFeedbackBeingRecorded, 0);
        FeedbackRecordingChanged?.Invoke(this, false);
    }

    private void OnFeedbackSemaphoreCreatedOrChanged()
    {
        if (!IsLoggingEnabledForCurrentVisualStudioInstance(_vsFeedbackSemaphoreFullPath))
        {
            // The semaphore file was created for another VS instance.
            return;
        }

        Interlocked.Exchange(ref _isFeedbackBeingRecorded, 1);
        FeedbackRecordingChanged?.Invoke(this, true);

    }

    private bool IsLoggingEnabledForCurrentVisualStudioInstance(string semaphoreFilePath)
    {
        try
        {
            if (_vsProcessStartTime > File.GetCreationTime(semaphoreFilePath))
            {
                // Semaphore file is older than the running instance of VS
                return false;
            }

            // Check the contents of the semaphore file to see if it's for this instance of VS.
            // Reading can hit contention so attempt a few times before bailing
            string? content = null;
            for (var i = 0; i < 5; i++)
            {
                try
                {
                    content = File.ReadAllText(semaphoreFilePath);
                }
                catch (IOException)
                {
                }

                if (content is not null)
                {
                    break;
                }
            }

            if (content is null)
            {
                return false;
            }

            var node = JsonNode.Parse(content);
            return node?["processIds"] is JsonArray pids
                && pids.Any(n => n?.GetValue<int>() == _vsProcessId);
        }
        catch
        {
            // Something went wrong opening or parsing the semaphore file - ignore it
            return false;
        }
    }
}
