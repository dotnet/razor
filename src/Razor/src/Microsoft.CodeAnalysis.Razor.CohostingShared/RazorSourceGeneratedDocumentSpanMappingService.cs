// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.CohostingShared;

[Export(typeof(IRazorSourceGeneratedDocumentSpanMappingService))]
[method: ImportingConstructor]
internal sealed class RazorSourceGeneratedDocumentSpanMappingService(IRemoteServiceInvoker remoteServiceInvoker) : IRazorSourceGeneratedDocumentSpanMappingService
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;

    public async Task<ImmutableArray<RazorMappedEditResult>> GetMappedTextChangesAsync(Document oldDocument, Document newDocument, CancellationToken cancellationToken)
    {
        // We have to get the text changes on this side, because we're dealing with changed source generated documents, and we can't
        // expect to transfer the Ids over to OOP and see the same changes
        var changes = await newDocument.GetTextChangesAsync(oldDocument, cancellationToken).ConfigureAwait(false);
        var changesArray = changes.ToImmutableArray();
        if (changesArray.IsDefaultOrEmpty)
        {
            return [];
        }

        return await _remoteServiceInvoker.TryInvokeAsync<IRemoteSpanMappingService, ImmutableArray<RazorMappedEditResult>>(
            oldDocument.Project.Solution,
            (service, solutionInfo, cancellationToken) => service.MapTextChangesAsync(solutionInfo, newDocument.Id, changesArray, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<ImmutableArray<RazorMappedSpanResult>> MapSpansAsync(Document document, ImmutableArray<TextSpan> spans, CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }
}
