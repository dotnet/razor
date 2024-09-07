// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.CodeActions;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using StreamJsonRpc;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Endpoints;

internal partial class RazorCustomMessageTarget
{
    [JsonRpcMethod(CustomMessageNames.RazorGetSymbolicInfoEndpointName, UseSingleObjectParameterDeserialization = true)]
    public async Task<MemberSymbolicInfo?> RazorGetSymbolicInfoAsync(GetSymbolicInfoParams request, CancellationToken cancellationToken)
    {
        var (synchronized, virtualDocumentSnapshot) = await TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(request.HostDocumentVersion, request.Document, cancellationToken);
        if (!synchronized || virtualDocumentSnapshot is null)
        {
            return null;
        }

        request.Document.Uri = virtualDocumentSnapshot.Uri;
        ReinvokeResponse<MemberSymbolicInfo?> response;

        try
        {
            response = await _requestInvoker.ReinvokeRequestOnServerAsync<GetSymbolicInfoParams, MemberSymbolicInfo?>(
                RazorLSPConstants.RoslynGetSymbolicInfoEndpointName,
                RazorLSPConstants.RazorCSharpLanguageServerName,
                request,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed at Endpoint: Failed to retrieve Razor component information.", ex);
        }

        return response.Result;
    }
}
