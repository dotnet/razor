// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed class RemoteFormattingService(in ServiceArgs args) : RazorDocumentServiceBase(in args), IRemoteFormattingService
{
    internal sealed class Factory : FactoryBase<IRemoteFormattingService>
    {
        protected override IRemoteFormattingService CreateService(in ServiceArgs args)
            => new RemoteFormattingService(in args);
    }

    private readonly IRazorFormattingService _foldingRangeService = args.ExportProvider.GetExportedValue<IRazorFormattingService>();

    public ValueTask<ImmutableArray<TextChange>> GetDocumentFormattingEditsAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId documentId,
        ImmutableArray<TextChange> htmlChanges,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            documentId,
            context => GetDocumentFormattingEditsAsync(context, htmlChanges, cancellationToken),
            cancellationToken);

    private async ValueTask<ImmutableArray<TextChange>> GetDocumentFormattingEditsAsync(
        RemoteDocumentContext context,
        ImmutableArray<TextChange> htmlChanges,
        CancellationToken cancellationToken)
    {
        var sourceText = await context.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
        var htmlEdits = htmlChanges.Select(sourceText.GetTextEdit).ToArray();

        var options = RazorFormattingOptions.Default;

        var edits = await _foldingRangeService.GetDocumentFormattingEditsAsync(context, htmlEdits, range: null, options, cancellationToken).ConfigureAwait(false);

        if (edits is null)
        {
            return ImmutableArray<TextChange>.Empty;
        }

        return edits.SelectAsArray(sourceText.GetTextChange);
    }
}
