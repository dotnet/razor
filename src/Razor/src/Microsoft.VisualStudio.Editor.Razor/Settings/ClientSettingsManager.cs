// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor.Settings;

[Export(typeof(IClientSettingsManager))]
internal class ClientSettingsManager : IClientSettingsManager, IDisposable
{
    public event EventHandler<ClientSettingsChangedEventArgs>? ClientSettingsChanged;
    public event EventHandler<bool>? FeedbackRecordingChanged;

    private readonly object _settingsUpdateLock = new();

    private readonly IAdvancedSettingsStorage? _advancedSettingsStorage;
    private readonly RazorGlobalOptions? _globalOptions;
    private FeedbackRecordingWatcher? _feedbackRecordingWatcher = new();

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

        _feedbackRecordingWatcher.FeedbackRecordingChanged += FeedbackRecordingWatcher_FeedbackRecordingChanged;
    }

    public ClientSettings ClientSettings { get; private set; }

    public bool IsFeedbackBeingRecorded => _feedbackRecordingWatcher?.IsFeedbackBeingRecorded ?? false;

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

    private void FeedbackRecordingWatcher_FeedbackRecordingChanged(object sender, bool e)
    {
        FeedbackRecordingChanged?.Invoke(this, e);
    }

    public void Dispose()
    {
        (var watcher, _feedbackRecordingWatcher) = (_feedbackRecordingWatcher, null);

        if (watcher is not null)
        {
            watcher.FeedbackRecordingChanged -= FeedbackRecordingWatcher_FeedbackRecordingChanged;
            watcher.Dispose();
        }
    }
}
