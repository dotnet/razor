// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using StreamJsonRpc;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

internal partial class RazorCustomMessageTarget
{
    [JsonRpcMethod(CustomMessageNames.RazorSimplifyTypeEndpointName, UseSingleObjectParameterDeserialization = true)]
    public async Task<string[]?> SimplifyTypeAsync(DelegatedSimplifyTypeNamesParams request, CancellationToken cancellationToken)
    {
        var (synchronized, virtualDocument) = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
            request.Identifier.Version,
            request.Identifier.TextDocumentIdentifier.Uri,
            cancellationToken).ConfigureAwait(false);
        if (!synchronized)
        {
            return null;
        }

        var simplifyTypeNamesParams = new SimplifyTypeNamesParams()
        {
            TextDocument = request.Identifier.TextDocumentIdentifier.WithUri(virtualDocument.Uri),
            PlacementTextDocument = request.CodeBehindIdentifier,
            FullyQualifiedTypeNames = request.FullyQualifiedTypeNames,
            AbsoluteIndex = request.AbsoluteIndex,
        };

        var textBuffer = virtualDocument.Snapshot.TextBuffer;
        var response = await _requestInvoker.ReinvokeRequestOnServerAsync<SimplifyTypeNamesParams, string[]?>(
            textBuffer,
            "textDocument/simplifyTypeNames",
            RazorLSPConstants.RazorCSharpLanguageServerName,
            simplifyTypeNamesParams,
            cancellationToken).ConfigureAwait(false);
        return response?.Response;
    }
}
