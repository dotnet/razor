// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed class RemoteDebugInfoService(in ServiceArgs args) : RazorDocumentServiceBase(in args), IRemoteDebugInfoService
{
    internal sealed class Factory : FactoryBase<IRemoteDebugInfoService>
    {
        protected override IRemoteDebugInfoService CreateService(in ServiceArgs args)
            => new RemoteDebugInfoService(in args);
    }

    private readonly IDocumentMappingService _documentMappingService = args.ExportProvider.GetExportedValue<IDocumentMappingService>();

    public ValueTask<LinePositionSpan?> ValidateBreakableRangeAsync(RazorPinnedSolutionInfoWrapper solutionInfo, DocumentId documentId, LinePositionSpan span, CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            documentId,
            context => ValidateBreakableRangeAsync(context, span, cancellationToken),
            cancellationToken);

    public async ValueTask<LinePositionSpan?> ValidateBreakableRangeAsync(RemoteDocumentContext context, LinePositionSpan span, CancellationToken cancellationToken)
    {
        var codeDocument = await context.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var csharpDocument = codeDocument.GetCSharpDocument();

        if (!_documentMappingService.TryMapToGeneratedDocumentRange(csharpDocument, span, out var mappedSpan))
        {
            return null;
        }

        var generatedDocument = await context.Snapshot.GetGeneratedDocumentAsync(cancellationToken).ConfigureAwait(false);

        var result = await ExternalAccess.Razor.Cohost.Handlers.ValidateBreakableRange.GetBreakableRangeAsync(generatedDocument, mappedSpan, cancellationToken).ConfigureAwait(false);
        if (result is { } csharpSpan &&
            _documentMappingService.TryMapToHostDocumentRange(codeDocument.GetCSharpDocument(), csharpSpan, MappingBehavior.Inclusive, out var hostSpan))
        {
            return hostSpan;
        }

        return null;
    }
}
