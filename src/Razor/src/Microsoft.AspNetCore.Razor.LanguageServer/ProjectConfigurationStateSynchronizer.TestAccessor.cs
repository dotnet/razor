// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal partial class ProjectConfigurationStateSynchronizer
{
    internal TestAccessor GetTestAccessor() => new(this);

    internal sealed class TestAccessor(ProjectConfigurationStateSynchronizer instance)
    {
        public Task WaitForProjectUpdatesToDrainAsync(CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                while (UpdatesRemaining() > 0)
                {
                    await Task.Yield();
                }

                int UpdatesRemaining()
                {
                    lock (instance._projectUpdates)
                    {
                        var count = instance._projectUpdates.Count;

                        if (count > 0)
                        {
                            foreach (var (_, updateItem) in instance._projectUpdates)
                            {
                                if (updateItem.Task is null || updateItem.Task.IsCompleted)
                                {
                                    count--;
                                }
                            }
                        }

                        return count;
                    }
                }
            },
            cancellationToken);
        }
    }
}
