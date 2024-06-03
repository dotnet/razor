// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Razor.Logging;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Razor;

[Export(typeof(ILspEditorFeatureDetector))]
internal sealed class LspEditorFeatureDetector : ILspEditorFeatureDetector
{
    private readonly IAsyncServiceProvider _serviceProvider;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly IUIContextService _uiContextService;
    private readonly RazorActivityLog _activityLog;
    private readonly AsyncLazy<bool> _useLegacyEditorLazy;

    [ImportingConstructor]
    public LspEditorFeatureDetector(
        IAsyncServiceProvider serviceProvider,
        JoinableTaskContext joinableTaskContext,
        IFeatureFlagService featureFlagService,
        IUIContextService uiContextService,
        RazorActivityLog activityLog)
    {
        _serviceProvider = serviceProvider;
        _featureFlagService = featureFlagService;
        _uiContextService = uiContextService;
        _activityLog = activityLog;
        _useLegacyEditorLazy = new(GetUseLegacyEditorValueAsync, joinableTaskContext.Factory);
    }

    private async Task<bool> GetUseLegacyEditorValueAsync()
    {
        _activityLog.LogInfo("Checking if LSP Editor is available");

        if (_featureFlagService.IsFeatureEnabled(WellKnownFeatureFlagNames.UseLegacyRazorEditor))
        {
            _activityLog.LogInfo("Using Legacy editor because the feature flag was set to true");
            return true;
        }

        var settingsManager = await _serviceProvider.GetFreeThreadedServiceAsync<SVsSettingsPersistenceManager, ISettingsManager>().ConfigureAwait(false);
        Assumes.Present(settingsManager);

        var useLegacyEditor = settingsManager.GetValueOrDefault<bool>(WellKnownSettingNames.UseLegacyASPNETCoreEditor);

        if (useLegacyEditor)
        {
            _activityLog.LogInfo("Using Legacy editor because the option was set to true");
        }
        else
        {
            _activityLog.LogInfo("LSP editor is available");
        }

        return useLegacyEditor;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the LSP-based editor is available.
    /// </summary>
    public bool IsLspEditorAvailable()
        => !_useLegacyEditorLazy.GetValue();

    /// <summary>
    /// Returns <see langword="true"/> if this is a LiveShare guest or a CodeSpaces client.
    /// </summary>
    public bool IsRemoteClient()
        => _uiContextService.IsActive(VSConstants.UICONTEXT.CloudEnvironmentConnected_guid) ||
           _uiContextService.IsActive(Guids.LiveShareGuestUIContextGuid);

    /// <summary>
    /// Returns <see langword="true"/> if this is a LiveShare host.
    /// </summary>
    public bool IsLiveShareHost()
        => _uiContextService.IsActive(Guids.LiveShareHostUIContextGuid);
}
