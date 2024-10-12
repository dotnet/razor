// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Response = Microsoft.CodeAnalysis.Razor.Remote.RemoteResponse<Microsoft.VisualStudio.LanguageServer.Protocol.VSInternalCompletionList?>;

namespace Microsoft.CodeAnalysis.Razor.Remote;

internal interface IRemoteCompletionService : IRemoteJsonService
{
    ValueTask<DocumentPositionInfo?> GetPositionInfoAsync(
        JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo,
        JsonSerializableDocumentId documentId,
        Position position,
        CancellationToken cancellationToken);

    ValueTask<Response> GetCompletionAsync(
        JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo,
        JsonSerializableDocumentId documentId,
        DocumentPositionInfo positionInfo,
        VSInternalCompletionContext completionContext,
        VSInternalClientCapabilities clientCapabilities,
        RazorCompletionOptions razorCompletionOptions,
        HashSet<string> existingHtmlCompletions,
        CancellationToken cancellationToken);
}
