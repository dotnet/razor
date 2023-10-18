// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using StreamJsonRpc;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

internal partial class RazorCustomMessageTarget
{
    // Called by the Razor Language Server to provide code actions from the platform.
    [JsonRpcMethod(CustomMessageNames.RazorMapCodeEndpoint, UseSingleObjectParameterDeserialization = true)]
    public async Task<WorkspaceEdit?> MapCodeAsync(DelegatedMapCodeParams request, CancellationToken cancellationToken)
    {
        var delegationDetails = await GetProjectedRequestDetailsAsync(request, cancellationToken).ConfigureAwait(false);
        if (delegationDetails is null)
        {
            return null;
        }

        ConvertCSharpFocusLocationUris(request.FocusLocations);

        var mappings = new MapCodeMapping()
        {
            TextDocument = request.Identifier.TextDocumentIdentifier.WithUri(delegationDetails.Value.ProjectedUri),
            Contents = request.Contents,
            FocusLocations = request.FocusLocations
        };

        var mapCodeParams = new MapCodeParams()
        {
            Mappings = [mappings]
        };

        var textBuffer = delegationDetails.Value.TextBuffer;

        try
        {
            var response = await _requestInvoker.ReinvokeRequestOnServerAsync<MapCodeParams, WorkspaceEdit?>(
                textBuffer,
                MapperMethods.WorkspaceMapCodeName,
                delegationDetails.Value.LanguageServerName,
                mapCodeParams,
                cancellationToken).ConfigureAwait(false);
            return response?.Response;
        }
        catch (RemoteMethodNotFoundException)
        {
            // C# and/or HTML haven't implemented handlers yet.
            return null;
        }
    }

    private void ConvertCSharpFocusLocationUris(Location[][] focusLocations)
    {
        // If the focus locations are in a C# context, map them to the C# document.
        foreach (var locationsByPriority in focusLocations)
        {
            foreach (var location in locationsByPriority)
            {
                if (location is null)
                {
                    continue;
                }

                if (!_documentManager.TryGetDocument(location.Uri, out var documentSnapshot))
                {
                    continue;
                }

                if (!documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var virtualDocument))
                {
                    continue;
                }

                location.Uri = virtualDocument.Uri;
            }
        }
    }
}
