// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Razor.Extensions;
using Microsoft.VisualStudio.Razor.Logging;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Razor;

[Export(typeof(ILspEditorFeatureDetector))]
internal class VisualStudioLSPEditorFeatureDetector : ILspEditorFeatureDetector, IDisposable
{
    private static readonly Guid s_liveShareHostUIContextGuid = Guid.Parse("62de1aa5-70b0-4934-9324-680896466fe1");
    private static readonly Guid s_liveShareGuestUIContextGuid = Guid.Parse("fd93f3eb-60da-49cd-af15-acda729e357e");

    private readonly JoinableTaskFactory _jtf;
    private readonly CancellationTokenSource _disposeTokenSource;
    private readonly AsyncLazy<bool> _lazyUseLegacyEditorTask;

    [ImportingConstructor]
    public VisualStudioLSPEditorFeatureDetector(
        IVsService<SVsFeatureFlags, IVsFeatureFlags> vsFeatureFlagsService,
        IVsService<SVsSettingsPersistenceManager, ISettingsManager> vsSettingsManagerService,
        JoinableTaskContext joinableTaskContext,
        RazorActivityLog activityLog)
    {
        _jtf = joinableTaskContext.Factory;
        _disposeTokenSource = new();

        _lazyUseLegacyEditorTask = new(() =>
             ComputeUseLegacyEditorAsync(vsFeatureFlagsService, vsSettingsManagerService, activityLog, _disposeTokenSource.Token),
             _jtf);
    }

    public void Dispose()
    {
        if (_disposeTokenSource.IsCancellationRequested)
        {
            return;
        }

        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();
    }

    private static async Task<bool> ComputeUseLegacyEditorAsync(
        IVsService<SVsFeatureFlags, IVsFeatureFlags> vsFeatureFlagsService,
        IVsService<SVsSettingsPersistenceManager, ISettingsManager> vsSettingsManagerService,
        RazorActivityLog activityLog,
        CancellationToken cancellationToken)
    {
        var vsFeatureFlags = await vsFeatureFlagsService.GetValueAsync(cancellationToken).ConfigureAwaitRunInline();
        if (vsFeatureFlags.IsFeatureEnabled(WellKnownFeatureFlagNames.UseLegacyRazorEditor, defaultValue: false))
        {
            activityLog.LogInfo($"Using Legacy Razor editor because the '{WellKnownFeatureFlagNames.UseLegacyRazorEditor}' feature flag is enabled.");
            return true;
        }

        var settingsManager = await vsSettingsManagerService.GetValueAsync(cancellationToken).ConfigureAwaitRunInline();
        var useLegacyEditorSetting = settingsManager.GetValueOrDefault<bool>(WellKnownSettingNames.UseLegacyASPNETCoreEditor);

        if (useLegacyEditorSetting)
        {
            activityLog.LogInfo($"Using Legacy Razor editor because the '{WellKnownSettingNames.UseLegacyASPNETCoreEditor}' setting is set to true.");
            return true;
        }

        activityLog.LogInfo($"Using LSP Razor editor.");
        return false;
    }

    public bool IsLSPEditorAvailable()
    {
        // This method is first called by out IFilePathToContentTypeProvider.TryGetContentTypeForFilePath(...) implementations.
        // We call AsyncLazy<T>.GetValue() below to get the value. If the work hasn't yet completed, we guard against a hidden+
        // JTF.Run(...) on a background thread by asserting the UI thread.

        if (!_lazyUseLegacyEditorTask.IsValueFactoryCompleted)
        {
            _jtf.AssertUIThread();
        }

        return !_lazyUseLegacyEditorTask.GetValue(_disposeTokenSource.Token);
    }

    public bool IsRemoteClient() => IsVSRemoteClient() || IsLiveShareGuest();

    public bool IsLiveShareHost()
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
