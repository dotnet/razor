// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.CodeAnalysis.Razor.Remote;

internal interface IRemoteMapCodeService : IRemoteJsonService
{
    ValueTask<WorkspaceEdit?> MapCodeAsync(JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo, VSInternalMapCodeMapping[] mappings, Guid mapCodeCorrelationId, CancellationToken cancellationToken);
}
