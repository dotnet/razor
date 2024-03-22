// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor.Settings;

[Export(typeof(IClientSettingsManager))]
internal class ClientSettingsManager : IClientSettingsManager, IDisposable
{
    public event EventHandler<ClientSettingsChangedEventArgs>? ClientSettingsChanged;

    private readonly object _settingsUpdateLock = new();
    private readonly IAdvancedSettingsStorage? _advancedSettingsStorage;
    private readonly RazorGlobalOptions? _globalOptions;
    private readonly string _vsFeedbackSemaphoreFullPath;
    private readonly int _vsProcessId;
    private readonly DateTime _vsProcessStartTime;
    private FileSystemWatcher? _vsFeedbackSemaphoreFileWatcher;
    private int _isFeedbackBeingRecorded;

    private const string VSFeedbackSemaphoreDir = @"Microsoft\VSFeedbackCollector";
    private const string VSFeedbackSemaphoreFileName = "feedback.recording.json";

    [ImportingConstructor]
    public ClientSettingsManager(
        [ImportMany] IEnumerable<IClientSettingsChangedTrigger> changeTriggers,
        [Import(AllowDefault = true)] IAdvancedSettingsStorage? advancedSettingsStorage = null,
        RazorGlobalOptions? globalOptions = null)
    {
        ClientSettings = ClientSettings.Default;

        // update Roslyn's global options (null in tests):
        if (globalOptions is not null)
        {
            globalOptions.TabSize = ClientSettings.ClientSpaceSettings.IndentSize;
            globalOptions.UseTabs = ClientSettings.ClientSpaceSettings.IndentWithTabs;
        }

        foreach (var changeTrigger in changeTriggers)
        {
            changeTrigger.Initialize(this);
        }

        _advancedSettingsStorage = advancedSettingsStorage;
        _globalOptions = globalOptions;

        if (_advancedSettingsStorage is not null)
        {
            Update(_advancedSettingsStorage.GetAdvancedSettings());
            _advancedSettingsStorage.OnChangedAsync(Update).Forget();
        }

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

    public ClientSettings ClientSettings { get; private set; }

    public bool IsFeedbackBeingRecorded => _isFeedbackBeingRecorded == 1;
    public event EventHandler<bool>? FeedbackRecordingChanged;

    public bool ShouldLog(LogLevel logLevel)
        => IsFeedbackBeingRecorded
        || logLevel >= ClientSettings.AdvancedSettings.LogLevel;

    public void Update(ClientSpaceSettings updatedSettings)
    {
        if (updatedSettings is null)
        {
            throw new ArgumentNullException(nameof(updatedSettings));
        }

        // update Roslyn's global options (null in tests):
        if (_globalOptions is not null)
        {
            _globalOptions.TabSize = updatedSettings.IndentSize;
            _globalOptions.UseTabs = updatedSettings.IndentWithTabs;
        }

        lock (_settingsUpdateLock)
        {
            UpdateSettings_NoLock(ClientSettings with { ClientSpaceSettings = updatedSettings });
        }
    }

    public void Update(ClientCompletionSettings updatedSettings)
    {
        if (updatedSettings is null)
        {
            throw new ArgumentNullException(nameof(updatedSettings));
        }

        lock (_settingsUpdateLock)
        {
            UpdateSettings_NoLock(ClientSettings with { ClientCompletionSettings = updatedSettings });
        }
    }

    public void Update(ClientAdvancedSettings advancedSettings)
    {
        if (advancedSettings is null)
        {
            throw new ArgumentNullException(nameof(advancedSettings));
        }

        lock (_settingsUpdateLock)
        {
            UpdateSettings_NoLock(ClientSettings with { AdvancedSettings = advancedSettings });
        }
    }

    private void UpdateSettings_NoLock(ClientSettings settings)
    {
        if (!ClientSettings.Equals(settings))
        {
            ClientSettings = settings;

            var args = new ClientSettingsChangedEventArgs(ClientSettings);
            ClientSettingsChanged?.Invoke(this, args);
        }
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

    public void Dispose()
    {
        (var fileWatcher, _vsFeedbackSemaphoreFileWatcher) = (_vsFeedbackSemaphoreFileWatcher, null);
        fileWatcher?.Dispose();
    }
}
