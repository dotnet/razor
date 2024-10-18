// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

internal partial class RazorProjectService
{
    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(RazorProjectService instance)
    {
        public ValueTask WaitForInitializationAsync()
            => instance.WaitForInitializationAsync();

        public async Task<ProjectKey> AddProjectAsync(HostProject hostProject, CancellationToken cancellationToken)
        {
            await instance.WaitForInitializationAsync().ConfigureAwait(false);

            return await instance._projectManager
                .UpdateAsync(
                    (updater, state) =>
                    {
                        var (service, hostProject) = state;
                        return service.AddProjectCore(updater, hostProject);
                    },
                    (instance, hostProject),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
