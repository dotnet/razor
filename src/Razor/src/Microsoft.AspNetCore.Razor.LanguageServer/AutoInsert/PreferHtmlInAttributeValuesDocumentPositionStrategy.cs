// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.AutoInsert;

// The main reason for this service is auto-insert of empty double quotes when a user types
// equals "=" after Blazor component attribute. We think this is Razor (correctly I guess)
// and wouldn't forward auto-insert request to HTML in this case. By essentially overriding
// language info here we allow the request to be sent over to HTML where it will insert empty
// double-quotes as it would for any other attribute value
internal class PreferHtmlInAttributeValuesDocumentPositionInfoStrategy : IDocumentPositionInfoStrategy
{
    public static IDocumentPositionInfoStrategy Instance { get; } = new PreferHtmlInAttributeValuesDocumentPositionInfoStrategy();

    public async Task<DocumentPositionInfo?> TryGetPositionInfoAsync(IRazorDocumentMappingService documentMappingService, DocumentContext documentContext, Position position, ILogger logger, CancellationToken cancellationToken)
    {
        var defaultDocumentPositionInfo = await DefaultDocumentPositionInfoStrategy.Instance.TryGetPositionInfoAsync(documentMappingService, documentContext, position, logger, cancellationToken).ConfigureAwait(false);
        if (defaultDocumentPositionInfo is null)
        {
            return null;
        }

        if (defaultDocumentPositionInfo.LanguageKind != RazorLanguageKind.Razor ||
            defaultDocumentPositionInfo.HostDocumentIndex < 1)
        {
            return defaultDocumentPositionInfo;
        }

        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var previousPosition = defaultDocumentPositionInfo.HostDocumentIndex - 1;
        var charBeforePosition = codeDocument.GetSourceText()[previousPosition];
        if (charBeforePosition != '=')
        {
            return defaultDocumentPositionInfo;
        }

        var owner = await documentContext.GetSyntaxNodeAsync(previousPosition, cancellationToken).ConfigureAwait(false);
        if (owner is null || owner.Kind != SyntaxKind.MarkupTagHelperAttribute)
        {
            return defaultDocumentPositionInfo;
        }

        return new DocumentPositionInfo(RazorLanguageKind.Html, defaultDocumentPositionInfo.Position, defaultDocumentPositionInfo.HostDocumentIndex);
    }
}
