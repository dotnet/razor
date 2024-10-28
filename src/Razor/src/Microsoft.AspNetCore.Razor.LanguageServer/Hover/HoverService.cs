// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Hover;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Hover;

internal sealed partial class HoverService(
    IProjectSnapshotManager projectManager,
    IDocumentMappingService documentMappingService,
    IClientCapabilitiesService clientCapabilitiesService) : IHoverService
{
    private readonly IProjectSnapshotManager _projectManager = projectManager;
    private readonly IDocumentMappingService _documentMappingService = documentMappingService;
    private readonly IClientCapabilitiesService _clientCapabilitiesService = clientCapabilitiesService;

    public async Task<VSInternalHover?> GetRazorHoverInfoAsync(DocumentContext documentContext, DocumentPositionInfo positionInfo, CancellationToken cancellationToken)
    {
        // HTML can still sometimes be handled by razor. For example hovering over
        // a component tag like <Counter /> will still be in an html context
        if (positionInfo.LanguageKind == RazorLanguageKind.CSharp)
        {
            return null;
        }

        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);

        // Sometimes what looks like a html attribute can actually map to C#, in which case its better to let Roslyn try to handle this.
        // We can only do this if we're in single server mode though, otherwise we won't be delegating to Roslyn at all
        if (_documentMappingService.TryMapToGeneratedDocumentPosition(codeDocument.GetCSharpDocument(), positionInfo.HostDocumentIndex, out _, out _))
        {
            return null;
        }

        var options = HoverDisplayOptions.From(_clientCapabilitiesService.ClientCapabilities);

        return await HoverFactory.GetHoverAsync(
            codeDocument,
            documentContext.FilePath,
            positionInfo.HostDocumentIndex,
            options,
            _projectManager.GetQueryOperations(),
            cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<VSInternalHover?> TranslateDelegatedResponseAsync(VSInternalHover? response, DocumentContext documentContext, DocumentPositionInfo positionInfo, CancellationToken cancellationToken)
    {
        if (response?.Range is null)
        {
            return response;
        }

        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);

        // If we don't include the originally requested position in our response, the client may not show it, so we extend the range to ensure it is in there.
        // eg for hovering at @bind-Value:af$$ter, we want to show people the hover for the Value property, so Roslyn will return to us the range for just the
        // portion of the attribute that says "Value".
        if (RazorSyntaxFacts.TryGetFullAttributeNameSpan(codeDocument, positionInfo.HostDocumentIndex, out var originalAttributeRange))
        {
            var sourceText = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
            response.Range = sourceText.GetRange(originalAttributeRange);
        }
        else if (positionInfo.LanguageKind == RazorLanguageKind.CSharp)
        {
            if (_documentMappingService.TryMapToHostDocumentRange(codeDocument.GetCSharpDocument(), response.Range, out var projectedRange))
            {
                response.Range = projectedRange;
            }
            else
            {
                // We couldn't remap the range back from Roslyn, but we have to do something with it, because it definitely won't
                // be correct, and if the Razor document is small, will be completely outside the valid range for the file, which
                // would cause the client to error.
                // Returning null here will still show the hover, just there won't be any extra visual indication, like
                // a background color, applied by the client.
                response.Range = null;
            }
        }

        return response;
    }
}
