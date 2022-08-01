// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions
{
    internal static class DocumentContextExtensions
    {
        internal static async Task<Projection> GetProjectionAsync(this DocumentContext documentContext, int absoluteIndex, RazorDocumentMappingService documentMappingService, CancellationToken cancellationToken)
        {
            var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
            var sourceText = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);

            sourceText.GetLineAndOffset(absoluteIndex, out var line, out var character);
            var projectedPosition = new Position(line, character);

            var languageKind = documentMappingService.GetLanguageKind(codeDocument, absoluteIndex, rightAssociative: false);
            if (languageKind == RazorLanguageKind.CSharp)
            {
                if (documentMappingService.TryMapToProjectedDocumentPosition(codeDocument, absoluteIndex, out var mappedPosition, out _))
                {
                    // For C# locations, we attempt to return the corresponding position
                    // within the projected document
                    projectedPosition = mappedPosition;
                }
                else
                {
                    // It no longer makes sense to think of this location as C#, since it doesn't
                    // correspond to any position in the projected document. This should not happen
                    // since there should be source mappings for all the C# spans.
                    languageKind = RazorLanguageKind.Razor;
                }
            }

            return new Projection(languageKind, projectedPosition, absoluteIndex);
        }
    }

    internal record Projection(RazorLanguageKind LanguageKind, Position Position, int AbsoluteIndex);
}
