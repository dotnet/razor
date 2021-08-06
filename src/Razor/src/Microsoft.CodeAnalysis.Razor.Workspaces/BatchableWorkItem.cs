// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Razor.Workspaces
{
    internal abstract class BatchableWorkItem
    {
        public abstract ValueTask ProcessAsync(CancellationToken cancellationToken);
    }
}
