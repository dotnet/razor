// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Razor;

[Export(typeof(IFeatureFlagService))]
[method: ImportingConstructor]
internal sealed class FeatureFlagService(IAsyncServiceProvider serviceProvider, JoinableTaskContext joinableTaskContext) : IFeatureFlagService
{
    private readonly JoinableTask<IVsFeatureFlags> _lazyFeatureFlags = joinableTaskContext.Factory.RunAsync(async () =>
    {
        var featureFlags = await serviceProvider.GetFreeThreadedServiceAsync<SVsFeatureFlags, IVsFeatureFlags>().ConfigureAwait(false);
        Assumes.Present(featureFlags);

        return featureFlags;
    });

    public bool IsFeatureEnabled(string featureName, bool defaultValue = false)
    {
        var featureFlags = _lazyFeatureFlags.Join();

        // IVsFeatureFlags is free-threaded but VSTHRD010 seems to be reported anyway.
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
        return featureFlags.IsFeatureEnabled(featureName, defaultValue);
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
    }
}
