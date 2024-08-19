// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.DocumentMapping;

internal interface IDocumentPositionInfoStrategy
{
    Task<DocumentPositionInfo?> TryGetPositionInfoAsync(
        IDocumentMappingService documentMappingService,
        DocumentContext documentContext,
        Position position,
        CancellationToken cancellationToken);
}
