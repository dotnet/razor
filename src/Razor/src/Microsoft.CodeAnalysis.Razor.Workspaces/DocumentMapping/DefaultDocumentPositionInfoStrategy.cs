// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.DocumentMapping;

internal sealed class DefaultDocumentPositionInfoStrategy : IDocumentPositionInfoStrategy
{
    public static IDocumentPositionInfoStrategy Instance { get; } = new DefaultDocumentPositionInfoStrategy();

    private DefaultDocumentPositionInfoStrategy()
    {
    }

    public async Task<DocumentPositionInfo?> TryGetPositionInfoAsync(
        IDocumentMappingService documentMappingService,
        DocumentContext documentContext,
        Position position,
        CancellationToken cancellationToken)
    {
        var sourceText = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
        if (!sourceText.TryGetAbsoluteIndex(position, out var absoluteIndex))
        {
            return null;
        }

        return await documentMappingService.GetPositionInfoAsync(documentContext, absoluteIndex, cancellationToken).ConfigureAwait(false);
    }
}
