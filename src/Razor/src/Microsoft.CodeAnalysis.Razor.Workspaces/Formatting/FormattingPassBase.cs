// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Formatting;

internal abstract class FormattingPassBase(IDocumentMappingService documentMappingService) : IFormattingPass
{
    protected IDocumentMappingService DocumentMappingService { get; } = documentMappingService;

    public abstract Task<FormattingResult> ExecuteAsync(FormattingContext context, FormattingResult result, CancellationToken cancellationToken);

    protected TextEdit[] RemapTextEdits(RazorCodeDocument codeDocument, TextEdit[] projectedTextEdits, RazorLanguageKind projectedKind)
    {
        if (projectedKind != RazorLanguageKind.CSharp)
        {
            // Non C# projections map directly to Razor. No need to remap.
            return projectedTextEdits;
        }

        if (codeDocument.IsUnsupported())
        {
            return [];
        }

        var edits = DocumentMappingService.GetHostDocumentEdits(codeDocument.GetCSharpDocument(), projectedTextEdits);

        return edits;
    }
}
