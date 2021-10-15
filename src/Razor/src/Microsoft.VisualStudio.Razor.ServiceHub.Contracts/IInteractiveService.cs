// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Razor.ServiceHub.Contracts
{
    public interface IInteractiveService
    {
        Task<bool> IsRunning(CancellationToken ct);

        Task<bool> Start(CancellationToken ct);
    }
}
