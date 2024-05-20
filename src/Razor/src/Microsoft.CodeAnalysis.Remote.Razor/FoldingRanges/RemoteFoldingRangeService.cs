// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Api;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor.FoldingRanges;
using Microsoft.CodeAnalysis.Razor.Protocol.Folding;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed class RemoteFoldingRangeService(
    IServiceBroker serviceBroker,
    IFoldingRangeService foldingRangeService,
    DocumentSnapshotFactory documentSnapshotFactory,
    IFilePathService filePathService)
    : RazorDocumentServiceBase(serviceBroker, documentSnapshotFactory), IRemoteFoldingRangeService
{
    private readonly IFoldingRangeService _foldingRangeService = foldingRangeService;
    private readonly IFilePathService _filePathService = filePathService;

    public ValueTask<ImmutableArray<RemoteFoldingRange>?> GetFoldingRangesAsync(RazorPinnedSolutionInfoWrapper solutionInfo, DocumentId id, ImmutableArray<RemoteFoldingRange> htmlRanges, CancellationToken cancellationToken)
        => RazorBrokeredServiceImplementation.RunServiceAsync(
            solutionInfo,
            ServiceBrokerClient,
            solution => GetFoldingRangesAsync(solution, id, htmlRanges, cancellationToken),
            cancellationToken);

    private async ValueTask<ImmutableArray<RemoteFoldingRange>?> GetFoldingRangesAsync(Solution solution, DocumentId razorDocumentId, ImmutableArray<RemoteFoldingRange> htmlRanges, CancellationToken cancellationToken)
    {
        var documentContext = CreateRazorDocumentContext(solution, razorDocumentId);
        if (documentContext is null)
        {
            return null;
        }

        var generatedDocument = await documentContext.GetGeneratedDocumentAsync(_filePathService, cancellationToken).ConfigureAwait(false);

        var csharpRanges = await ExternalAccess.Razor.Cohost.Handlers.FoldingRanges.GetFoldingRangesAsync(generatedDocument, cancellationToken).ConfigureAwait(false);

        var convertedCSharp = csharpRanges.SelectAsArray(ToFoldingRange);
        var convertedHtml = htmlRanges.SelectAsArray(RemoteFoldingRange.ToFoldingRange);

        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        return _foldingRangeService.GetFoldingRanges(codeDocument, convertedCSharp, convertedHtml, cancellationToken)
            .SelectAsArray(RemoteFoldingRange.FromFoldingRange);
    }

    public static FoldingRange ToFoldingRange(Roslyn.LanguageServer.Protocol.FoldingRange r)
        => new FoldingRange
        {
            StartLine = r.StartLine,
            StartCharacter = r.StartCharacter,
            EndLine = r.EndLine,
            EndCharacter = r.EndCharacter,
            Kind = r.Kind is { } kind ? new FoldingRangeKind(kind.Value) : null,
            CollapsedText = r.CollapsedText
        };
}
