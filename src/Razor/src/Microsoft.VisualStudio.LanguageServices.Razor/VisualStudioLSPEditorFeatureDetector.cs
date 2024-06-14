// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Razor.Logging;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.Razor;

[Export(typeof(LSPEditorFeatureDetector))]
internal class VisualStudioLSPEditorFeatureDetector : LSPEditorFeatureDetector
{
    private const string LegacyRazorEditorFeatureFlag = "Razor.LSP.LegacyEditor";
    private const string DotNetCoreCSharpCapability = "CSharp&CPS";
    private const string LegacyRazorEditorCapability = "LegacyRazorEditor";
    private const string UseLegacyASPNETCoreEditorSetting = "TextEditor.HTML.Specific.UseLegacyASPNETCoreRazorEditor";

    private static readonly Guid s_liveShareHostUIContextGuid = Guid.Parse("62de1aa5-70b0-4934-9324-680896466fe1");
    private static readonly Guid s_liveShareGuestUIContextGuid = Guid.Parse("fd93f3eb-60da-49cd-af15-acda729e357e");

    private readonly AggregateProjectCapabilityResolver _projectCapabilityResolver;
    private readonly Lazy<IVsUIShellOpenDocument> _vsUIShellOpenDocument;
    private readonly Lazy<bool> _useLegacyEditor;

    private readonly RazorActivityLog _activityLog;

    [ImportingConstructor]
    public VisualStudioLSPEditorFeatureDetector(AggregateProjectCapabilityResolver projectCapabilityResolver, RazorActivityLog activityLog)
    {
        _projectCapabilityResolver = projectCapabilityResolver;
        _vsUIShellOpenDocument = new Lazy<IVsUIShellOpenDocument>(() =>
        {
            var shellOpenDocument = (IVsUIShellOpenDocument)ServiceProvider.GlobalProvider.GetService(typeof(SVsUIShellOpenDocument));
            Assumes.Present(shellOpenDocument);

            return shellOpenDocument;
        });

        _useLegacyEditor = new Lazy<bool>(() =>
        {
            var featureFlags = (IVsFeatureFlags)Package.GetGlobalService(typeof(SVsFeatureFlags));
            var legacyEditorFeatureFlagEnabled = featureFlags.IsFeatureEnabled(LegacyRazorEditorFeatureFlag, defaultValue: false);
            if (legacyEditorFeatureFlagEnabled)
            {
                activityLog.LogInfo($"Using Legacy Razor editor because the '{LegacyRazorEditorFeatureFlag}' feature flag is enabled.");
                return true;
            }

            var settingsManager = (ISettingsManager)ServiceProvider.GlobalProvider.GetService(typeof(SVsSettingsPersistenceManager));
            Assumes.Present(settingsManager);

            var useLegacyEditorSetting = settingsManager.GetValueOrDefault<bool>(UseLegacyASPNETCoreEditorSetting);

            if (useLegacyEditorSetting)
            {
                activityLog.LogInfo($"Using Legacy Razor editor because the '{UseLegacyASPNETCoreEditorSetting}' setting is set to true.");
                return true;
            }

            activityLog.LogInfo($"Using LSP Razor editor.");
            return false;
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
    private protected virtual bool ProjectSupportsLSPEditor(string documentMoniker, IVsHierarchy? hierarchy)
    {
        if (hierarchy is null)
        {
            var hr = _vsUIShellOpenDocument.Value.IsDocumentInAProject(documentMoniker, out var uiHierarchy, out _, out _, out _);
            hierarchy = uiHierarchy;
            if (!ErrorHandler.Succeeded(hr) || hierarchy is null)
            {
                if (!ErrorHandler.Succeeded(hr))
                {
                    _activityLog.LogWarning($"Project does not support LSP Editor beccause {nameof(_vsUIShellOpenDocument.Value.IsDocumentInAProject)} failed with exit code {hr}");
                }
                else if (hierarchy is null)
                {
                    _activityLog.LogWarning($"Project does not support LSP Editor because {nameof(hierarchy)} is null");
                }

                return false;
            }
        }

        // We alow projects to specifically opt-out of the legacy Razor editor because there are legacy scenarios which would rely on behind-the-scenes
        // opt-out mechanics to enable the .NET Core editor in non-.NET Core scenarios. Therefore, we need a similar mechanic to continue supporting
        // those types of scenarios for the new .NET Core Razor editor.
        if (_projectCapabilityResolver.HasCapability(documentMoniker, hierarchy, LegacyRazorEditorCapability))
        {
            _activityLog.LogInfo($"Project does not support LSP Editor because '{documentMoniker}' has Capability {LegacyRazorEditorCapability}");
            // CPS project that requires the legacy editor
            return false;
        }

        if (_projectCapabilityResolver.HasCapability(documentMoniker, hierarchy, DotNetCoreCSharpCapability))
        {
            // .NET Core project that supports C#
            return true;
        }

        _activityLog.LogInfo($"Project {documentMoniker} does not support LSP Editor because it does not have the {DotNetCoreCSharpCapability} capability.");
        // Not a C# .NET Core project. This typically happens for legacy Razor scenarios
        return false;
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
