﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Protocol;
using StreamJsonRpc;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Endpoints;

internal partial class RazorCustomMessageTarget
{
    [JsonRpcMethod(CustomMessageNames.RazorValidateBreakpointRangeName, UseSingleObjectParameterDeserialization = true)]
    public async Task<LspRange?> ValidateBreakpointRangeAsync(DelegatedValidateBreakpointRangeParams request, CancellationToken cancellationToken)
    {
        var delegationDetails = await GetProjectedRequestDetailsAsync(request, cancellationToken).ConfigureAwait(false);
        if (delegationDetails is null)
        {
            return default;
        }

        var validateBreakpointRangeParams = new VSInternalValidateBreakableRangeParams
        {
            TextDocument = request.Identifier.TextDocumentIdentifier.WithUri(delegationDetails.Value.ProjectedUri),
            Range = request.ProjectedRange
        };

        var response = await _requestInvoker.ReinvokeRequestOnServerAsync<VSInternalValidateBreakableRangeParams, LspRange?>(
            delegationDetails.Value.TextBuffer,
            VSInternalMethods.TextDocumentValidateBreakableRangeName,
            delegationDetails.Value.LanguageServerName,
            validateBreakpointRangeParams,
            cancellationToken).ConfigureAwait(false);
        return response?.Response;
    }
}
