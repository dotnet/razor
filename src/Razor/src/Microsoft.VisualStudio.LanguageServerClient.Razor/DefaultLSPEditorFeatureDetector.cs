// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    [Shared]
    [Export(typeof(LSPEditorFeatureDetector))]
    internal class DefaultLSPEditorFeatureDetector : LSPEditorFeatureDetector
    {
        private const string LegacyRazorEditorFeatureFlag = "Razor.LSP.LegacyEditor";
        private const string DotNetCoreCSharpCapability = "CSharp&CPS";
        private const string LegacyRazorEditorCapability = "LegacyRazorEditor";
        private const string UseLegacyASPNETCoreEditorSetting = "TextEditor.HTML.Specific.UseLegacyASPNETCoreRazorEditor";

        private static readonly Guid s_liveShareHostUIContextGuid = Guid.Parse("62de1aa5-70b0-4934-9324-680896466fe1");
        private static readonly Guid s_liveShareGuestUIContextGuid = Guid.Parse("fd93f3eb-60da-49cd-af15-acda729e357e");

        private readonly ProjectHierarchyInspector _projectHierarchyInspector;
        private readonly Lazy<IVsUIShellOpenDocument> _vsUIShellOpenDocument;
        private readonly Lazy<bool> _useLegacyEditor;

        [ImportingConstructor]
        public DefaultLSPEditorFeatureDetector(ProjectHierarchyInspector projectHierarchyInspector)
        {
            if (projectHierarchyInspector is null)
            {
                throw new ArgumentNullException(nameof(projectHierarchyInspector));
            }

            _projectHierarchyInspector = projectHierarchyInspector;
            _vsUIShellOpenDocument = new Lazy<IVsUIShellOpenDocument>(() =>
            {
                var shellOpenDocument = (IVsUIShellOpenDocument)ServiceProvider.GlobalProvider.GetService(typeof(SVsUIShellOpenDocument));
                Assumes.Present(shellOpenDocument);

                return shellOpenDocument;
            });

            _useLegacyEditor = new Lazy<bool>(() =>
            {
                var featureFlags = (IVsFeatureFlags)AsyncPackage.GetGlobalService(typeof(SVsFeatureFlags));
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
        }

        // Test constructor
        internal DefaultLSPEditorFeatureDetector()
        {
        }

        public override bool IsLSPEditorAvailable(string documentMoniker, object hierarchy)
        {
            if (documentMoniker == null)
            {
                return false;
            }

            if (!IsLSPEditorAvailable())
            {
                return false;
            }

            var ivsHierarchy = hierarchy as IVsHierarchy;
            if (!ProjectSupportsLSPEditor(documentMoniker, ivsHierarchy))
            {
                // Current project hierarchy doesn't support the LSP Razor editor
                return false;
            }

            return true;
        }

        public override bool IsLSPEditorAvailable() => !_useLegacyEditor.Value;

        public override bool IsRemoteClient() => IsVSRemoteClient() || IsLiveShareGuest();

        public override bool IsLiveShareHost()
        {
            var context = UIContext.FromUIContextGuid(s_liveShareHostUIContextGuid);
            return context.IsActive;
        }

        // Private protected virtual for testing
        private protected virtual bool ProjectSupportsLSPEditor(string documentMoniker, IVsHierarchy hierarchy)
        {
            if (hierarchy == null)
            {
                var hr = _vsUIShellOpenDocument.Value.IsDocumentInAProject(documentMoniker, out var uiHierarchy, out _, out _, out _);
                hierarchy = uiHierarchy;
                if (!ErrorHandler.Succeeded(hr) || hierarchy == null)
                {
                    return false;
                }
            }

            if (_projectHierarchyInspector.HasCapability(documentMoniker, hierarchy, LegacyRazorEditorCapability))
            {
                // CPS project that requires the legacy editor
                return false;
            }

            if (_projectHierarchyInspector.HasCapability(documentMoniker, hierarchy, DotNetCoreCSharpCapability))
            {
                // .NET Core project that supports C#
                return true;
            }

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
}
