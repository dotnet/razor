// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor.Editor;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.VisualStudio.Editor.Razor;

[System.Composition.Shared]
[Export(typeof(IClientSettingsManager))]
internal class EditorSettingsManager : IClientSettingsManager
{
    public event EventHandler<ClientSettingsChangedEventArgs>? Changed;

    private readonly object _settingsUpdateLock = new();
    private readonly IAdvancedSettingsStorage? _advancedSettingsStorage;
    private readonly RazorGlobalOptions? _globalOptions;
    private ClientSettings _settings;

    [ImportingConstructor]
    public EditorSettingsManager(
        [ImportMany] IEnumerable<EditorSettingsChangedTrigger> editorSettingsChangeTriggers,
        IAdvancedSettingsStorage? advancedSettingsStorage = null,
        RazorGlobalOptions? globalOptions = null)
    {
        _settings = ClientSettings.Default;

        // update Roslyn's global options (null in tests):
        if (globalOptions is not null)
        {
            globalOptions.TabSize = _settings.EditorSettings.IndentSize;
            globalOptions.UseTabs = _settings.EditorSettings.IndentWithTabs;
        }

        foreach (var changeTrigger in editorSettingsChangeTriggers)
        {
            changeTrigger.Initialize(this);
        }

        _advancedSettingsStorage = advancedSettingsStorage;
        _globalOptions = globalOptions;

        if (_advancedSettingsStorage is not null)
        {
            _advancedSettingsStorage.Changed += AdvancedSettingsChanged;
        }
    }

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
            UpdateSettings_NoLock(_settings with { EditorSettings = updatedSettings });
        }
    }

    public ClientSettings GetClientSettings() => _settings;

    public void Update(ClientAdvancedSettings advancedSettings)
    {
        if (advancedSettings is null)
        {
            throw new ArgumentNullException(nameof(advancedSettings));
        }

        lock (_settingsUpdateLock)
        {
            UpdateSettings_NoLock(_settings with { AdvancedSettings = advancedSettings });
        }
    }

    private void AdvancedSettingsChanged(object sender, ClientAdvancedSettingsChangedEventArgs e) => Update(e.Settings);

    private void UpdateSettings_NoLock(ClientSettings settings)
    {
        if (!_settings.Equals(settings))
        {
            _settings = settings;

            var args = new ClientSettingsChangedEventArgs(_settings);
            Changed?.Invoke(this, args);
        }
    }
}
