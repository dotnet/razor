// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.DocumentExcerpt;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.VisualStudio.Razor.DynamicFiles;

internal abstract class DocumentExcerptService : IRazorDocumentExcerptServiceImplementation
{
    async Task<RazorExcerptResult?> IRazorDocumentExcerptServiceImplementation.TryExcerptAsync(
        Document document,
        TextSpan span,
        RazorExcerptMode mode,
        RazorClassificationOptionsWrapper options,
        CancellationToken cancellationToken)
    {
        var result = await TryGetExcerptInternalAsync(document, span, (ExcerptModeInternal)mode, options, cancellationToken).ConfigureAwait(false);
        return result?.ToExcerptResult();
    }

    internal abstract Task<ExcerptResultInternal?> TryGetExcerptInternalAsync(
        Document document,
        TextSpan span,
        ExcerptModeInternal mode,
        RazorClassificationOptionsWrapper options,
        CancellationToken cancellationToken);
}
