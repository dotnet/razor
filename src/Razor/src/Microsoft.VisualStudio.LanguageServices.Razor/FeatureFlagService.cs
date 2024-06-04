// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Razor;

[Export(typeof(IFeatureFlagService))]
[method: ImportingConstructor]
internal sealed class FeatureFlagService(
    [Import(typeof(SAsyncServiceProvider))] IAsyncServiceProvider serviceProvider,
    JoinableTaskContext joinableTaskContext)
    : IFeatureFlagService
{
    private readonly JoinableTask<IVsFeatureFlags> _getVsFeatureFlagsTask = joinableTaskContext.Factory.RunAsync(
        () => GetVsFeatureFlagsAsync(serviceProvider));

    private static async Task<IVsFeatureFlags> GetVsFeatureFlagsAsync(IAsyncServiceProvider serviceProvider)
    {
        var featureFlags = await serviceProvider.GetFreeThreadedServiceAsync<SVsFeatureFlags, IVsFeatureFlags>().ConfigureAwait(false);
        Assumes.Present(featureFlags);

        return featureFlags;
    }

    public bool IsFeatureEnabled(string featureName, bool defaultValue = false)
    {
        var vsFeatureFlags = _getVsFeatureFlagsTask.Join();

        // IVsFeatureFlags is free-threaded but VSTHRD010 seems to be reported anyway.
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
        return vsFeatureFlags.IsFeatureEnabled(featureName, defaultValue);
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
    }
}
