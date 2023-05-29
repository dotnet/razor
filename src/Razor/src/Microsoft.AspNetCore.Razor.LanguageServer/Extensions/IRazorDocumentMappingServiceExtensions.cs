// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal static class IRazorDocumentMappingServiceExtensions
{
    public static async Task<Projection> GetProjectionAsync(this IRazorDocumentMappingService service, DocumentContext documentContext, int absoluteIndex, CancellationToken cancellationToken)
    {
        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var sourceText = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);

        sourceText.GetLineAndOffset(absoluteIndex, out var line, out var character);
        var projectedPosition = new Position(line, character);

        var languageKind = service.GetLanguageKind(codeDocument, absoluteIndex, rightAssociative: false);
        if (languageKind is not RazorLanguageKind.Razor)
        {
            var generatedDocument = languageKind is RazorLanguageKind.CSharp
                ? (IRazorGeneratedDocument)codeDocument.GetCSharpDocument()
                : codeDocument.GetHtmlDocument();
            if (service.TryMapToProjectedDocumentPosition(generatedDocument, absoluteIndex, out var mappedPosition, out _))
            {
                // For C# locations, we attempt to return the corresponding position
                // within the projected document
                projectedPosition = mappedPosition;
            }
            else
            {
                // It no longer makes sense to think of this location as C# or Html, since it doesn't
                // correspond to any position in the projected document. This should not happen
                // since there should be source mappings for all the C# spans.
                languageKind = RazorLanguageKind.Razor;
            }
        }

        return new Projection(languageKind, projectedPosition, absoluteIndex);
    }

    public static async Task<Projection?> TryGetProjectionAsync(this IRazorDocumentMappingService service, DocumentContext documentContext, Position position, ILogger logger, CancellationToken cancellationToken)
    {
        var sourceText = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
        if (!position.TryGetAbsoluteIndex(sourceText, logger, out var absoluteIndex))
        {
            return null;
        }

        return await GetProjectionAsync(service, documentContext, absoluteIndex, cancellationToken).ConfigureAwait(false);
    }
}
