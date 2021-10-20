// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Razor.ServiceHub.Contracts
{
    public interface IInteractiveService
    {
        Task<bool> IsRunning(CancellationToken ct);

        Task<bool> Start(Stream input, Stream output, CancellationToken ct);

        Task<bool> Shutdown();
    }
}
