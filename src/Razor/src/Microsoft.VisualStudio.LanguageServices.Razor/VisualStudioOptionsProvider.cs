// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Settings;

namespace Microsoft.VisualStudio.Razor;

internal sealed class VisualStudioOptionsProvider(IVsFeatureFlags vsFeatureFlags, ISettingsManager settingsManager) : IVisualStudioOptionsProvider
{
    private readonly IVsFeatureFlags _vsFeatureFlags = vsFeatureFlags;
    private readonly ISettingsManager _settingsManager = settingsManager;

    public bool IsFeatureEnabled(string name, bool defaultValue = false)
    {
        // IVsFeatureFlags is free-threaded but VSTHRD010 seems to be reported anyway.
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
        return _vsFeatureFlags.IsFeatureEnabled(name, defaultValue);
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
    }

    [return: MaybeNull]
    public T GetValueOrDefault<T>(string name, [AllowNull] T defaultValue = default)
    {
        return _settingsManager.GetValueOrDefault(name, defaultValue);
    }
}
