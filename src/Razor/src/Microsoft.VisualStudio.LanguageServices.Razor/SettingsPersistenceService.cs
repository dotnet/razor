// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Razor;

[Export(typeof(ISettingsPersistenceService))]
[method: ImportingConstructor]
internal class SettingsPersistenceService(
    [Import(typeof(SAsyncServiceProvider))] IAsyncServiceProvider serviceProvider,
    JoinableTaskContext joinableTaskContext)
    : ISettingsPersistenceService
{
    private readonly JoinableTask<ISettingsManager> _getSettingsManagerTask = joinableTaskContext.Factory.RunAsync(
        () => GetSettingsManagerAsync(serviceProvider));

    private static async Task<ISettingsManager> GetSettingsManagerAsync(IAsyncServiceProvider serviceProvider)
    {
        var settingsManager = await serviceProvider.GetFreeThreadedServiceAsync<SVsSettingsPersistenceManager, ISettingsManager>().ConfigureAwait(false);
        Assumes.Present(settingsManager);

        return settingsManager;
    }

    [return: MaybeNull]
    public T GetValueOrDefault<T>(string name, [AllowNull] T defaultValue = default)
    {
        var settingsManager = _getSettingsManagerTask.Join();

        return settingsManager.GetValueOrDefault(name, defaultValue);
    }
}
