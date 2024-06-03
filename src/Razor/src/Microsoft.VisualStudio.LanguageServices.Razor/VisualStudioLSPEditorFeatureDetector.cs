// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Razor.Logging;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualStudio.Razor;

[Export(typeof(LspEditorFeatureDetector))]
internal class VisualStudioLSPEditorFeatureDetector : LspEditorFeatureDetector
{
    private const string LegacyRazorEditorFeatureFlag = "Razor.LSP.LegacyEditor";
    private const string UseLegacyASPNETCoreEditorSetting = "TextEditor.HTML.Specific.UseLegacyASPNETCoreRazorEditor";

    private static readonly Guid s_liveShareHostUIContextGuid = Guid.Parse("62de1aa5-70b0-4934-9324-680896466fe1");
    private static readonly Guid s_liveShareGuestUIContextGuid = Guid.Parse("fd93f3eb-60da-49cd-af15-acda729e357e");

    private readonly Lazy<bool> _useLegacyEditor;

    private readonly RazorActivityLog _activityLog;

    [ImportingConstructor]
    public VisualStudioLSPEditorFeatureDetector(RazorActivityLog activityLog)
    {
        _useLegacyEditor = new Lazy<bool>(() =>
        {
            var featureFlags = (IVsFeatureFlags)Package.GetGlobalService(typeof(SVsFeatureFlags));
            var legacyEditorFeatureFlagEnabled = featureFlags.IsFeatureEnabled(LegacyRazorEditorFeatureFlag, defaultValue: false);
            if (legacyEditorFeatureFlagEnabled)
            {
                return true;
            }

            var settingsManager = (ISettingsManager)ServiceProvider.GlobalProvider.GetService(typeof(SVsSettingsPersistenceManager));
            Assumes.Present(settingsManager);

            var useLegacyEditor = settingsManager.GetValueOrDefault<bool>(UseLegacyASPNETCoreEditorSetting);
            return useLegacyEditor;
        });

        _activityLog = activityLog;
    }

    [Obsolete("Test constructor")]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    internal VisualStudioLSPEditorFeatureDetector()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {
    }

    public override bool IsLSPEditorAvailable() => !_useLegacyEditor.Value;

    public override bool IsRemoteClient() => IsVSRemoteClient() || IsLiveShareGuest();

    public override bool IsLiveShareHost()
    {
        var context = UIContext.FromUIContextGuid(s_liveShareHostUIContextGuid);
        return context.IsActive;
    }

    // Private protected virtual for testing
    private protected virtual bool IsVSRemoteClient()
    {
        var context = UIContext.FromUIContextGuid(VSConstants.UICONTEXT.CloudEnvironmentConnected_guid);
        return context.IsActive;
    }

    // Private protected virtual for testing
    private protected virtual bool IsLiveShareGuest()
    {
        var context = UIContext.FromUIContextGuid(s_liveShareGuestUIContextGuid);
        return context.IsActive;
    }
}
