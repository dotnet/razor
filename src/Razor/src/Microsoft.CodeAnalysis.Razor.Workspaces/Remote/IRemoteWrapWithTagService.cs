// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Remote;

internal interface IRemoteWrapWithTagService
{
    ValueTask<RemoteResponse<bool>> IsValidWrapWithTagLocationAsync(RazorPinnedSolutionInfoWrapper solutionInfo, DocumentId razorDocumentId, LspRange range, CancellationToken cancellationToken);
}