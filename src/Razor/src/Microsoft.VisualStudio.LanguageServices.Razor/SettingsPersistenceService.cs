// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Razor;

[Export(typeof(ISettingsPersistenceService))]
[method: ImportingConstructor]
internal class SettingsPersistenceService(IAsyncServiceProvider serviceProvider, JoinableTaskContext joinableTaskContext) : ISettingsPersistenceService
{
    private readonly JoinableTask<ISettingsManager> _settingsManagerTask = joinableTaskContext.Factory.RunAsync(async () =>
    {
        var settingsManager = await serviceProvider.GetFreeThreadedServiceAsync<SVsSettingsPersistenceManager, ISettingsManager>().ConfigureAwait(false);
        Assumes.Present(settingsManager);

        return settingsManager;
    });

    [return: MaybeNull]
    public T GetValueOrDefault<T>(string name, [AllowNull] T defaultValue = default)
    {
        var settingsManager = _settingsManagerTask.Join();

        return settingsManager.GetValueOrDefault(name, defaultValue);
    }
}
