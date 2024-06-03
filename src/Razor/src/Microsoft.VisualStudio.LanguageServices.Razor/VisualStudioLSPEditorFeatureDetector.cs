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

[Export(typeof(LspEditorFeatureDetector))]
internal class VisualStudioLSPEditorFeatureDetector : LspEditorFeatureDetector
{
    private const string LegacyRazorEditorFeatureFlag = "Razor.LSP.LegacyEditor";
    private const string UseLegacyASPNETCoreEditorSetting = "TextEditor.HTML.Specific.UseLegacyASPNETCoreRazorEditor";

    private readonly IAsyncServiceProvider _serviceProvider;
    private readonly IUIContextService _uiContextService;
    private readonly RazorActivityLog _activityLog;
    private readonly AsyncLazy<bool> _useLegacyEditorLazy;

    [ImportingConstructor]
    public VisualStudioLSPEditorFeatureDetector(
        IAsyncServiceProvider serviceProvider,
        JoinableTaskContext joinableTaskContext,
        IUIContextService uiContextService,
        RazorActivityLog activityLog)
    {
        _serviceProvider = serviceProvider;
        _uiContextService = uiContextService;
        _activityLog = activityLog;
        _useLegacyEditorLazy = new(GetLegacyEditorOptionAsync, joinableTaskContext.Factory);
    }

    private async Task<bool> GetLegacyEditorOptionAsync()
    {
        _activityLog.LogInfo("Checking if LSP Editor is available");

        var featureFlags = await _serviceProvider.GetFreeThreadedServiceAsync<SVsFeatureFlags, IVsFeatureFlags>().ConfigureAwait(false);
        Assumes.Present(featureFlags);

        // IVsFeatureFlags is free-threaded but VSTHRD010 seems to be reported anyway.
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
        if (featureFlags.IsFeatureEnabled(LegacyRazorEditorFeatureFlag, defaultValue: false))
        {
            _activityLog.LogInfo("Using Legacy editor because the feature flag was set to true");
            return true;
        }
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread

        var settingsManager = await _serviceProvider.GetFreeThreadedServiceAsync<SVsSettingsPersistenceManager, ISettingsManager>().ConfigureAwait(false);
        Assumes.Present(settingsManager);

        var useLegacyEditor = settingsManager.GetValueOrDefault<bool>(UseLegacyASPNETCoreEditorSetting);

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

    public override bool IsLSPEditorAvailable()
        => !_useLegacyEditorLazy.GetValue();

    public override bool IsRemoteClient()
        => _uiContextService.IsActive(VSConstants.UICONTEXT.CloudEnvironmentConnected_guid) ||
           _uiContextService.IsActive(Guids.LiveShareGuestUIContextGuid);

    public override bool IsLiveShareHost()
        => _uiContextService.IsActive(Guids.LiveShareHostUIContextGuid);
}
