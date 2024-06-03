// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Razor.Logging;

namespace Microsoft.VisualStudio.Razor;

[Export(typeof(ILspEditorFeatureDetector))]
[method: ImportingConstructor]
internal sealed class LspEditorFeatureDetector(
    IFeatureFlagService featureFlagService,
    ISettingsPersistenceService settingsPersistenceService,
    IUIContextService uiContextService,
    RazorActivityLog activityLog) : ILspEditorFeatureDetector
{
    private readonly IUIContextService _uiContextService = uiContextService;
    private readonly Lazy<bool> _useLegacyEditorLazy = new(() => GetUseLegacyEditorValue(featureFlagService, settingsPersistenceService, activityLog));

    private static bool GetUseLegacyEditorValue(
        IFeatureFlagService featureFlagService,
        ISettingsPersistenceService settingsPersistenceService,
        RazorActivityLog activityLog)
    {
        activityLog.LogInfo("Checking if LSP Editor is available");

        if (featureFlagService.IsFeatureEnabled(WellKnownFeatureFlagNames.UseLegacyRazorEditor))
        {
            activityLog.LogInfo("Using Legacy editor because the feature flag was set to true");
            return true;
        }

        var useLegacyEditor = settingsPersistenceService.GetValueOrDefault<bool>(WellKnownSettingNames.UseLegacyASPNETCoreEditor);

        if (useLegacyEditor)
        {
            activityLog.LogInfo("Using Legacy editor because the option was set to true");
        }
        else
        {
            activityLog.LogInfo("LSP editor is available");
        }

        return useLegacyEditor;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the LSP-based editor is available.
    /// </summary>
    public bool IsLspEditorAvailable()
        => !_useLegacyEditorLazy.Value;

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
