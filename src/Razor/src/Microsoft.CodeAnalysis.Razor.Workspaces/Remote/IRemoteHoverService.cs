// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using LspHover = Roslyn.LanguageServer.Protocol.Hover;

namespace Microsoft.CodeAnalysis.Razor.Remote;

internal interface IRemoteHoverService : IRemoteJsonService
{
    ValueTask<RemoteResponse<LspHover?>> GetHoverAsync(
        JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo,
        JsonSerializableDocumentId documentId,
        Position position,
        CancellationToken cancellationToken);
}
