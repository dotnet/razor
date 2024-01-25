// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Hover;

internal interface IHoverInfoService
{
    VSInternalHover? GetHoverInfo(string documentFilePath, RazorCodeDocument codeDocument, SourceLocation location, VSInternalClientCapabilities clientCapabilities);
    Task<VSInternalHover?> GetHoverInfoAsync(VersionedDocumentContext versionedDocumentContext, DocumentPositionInfo positionInfo, Position position, IRazorDocumentMappingService documentMappingService, VSInternalClientCapabilities? _clientCapabilities, CancellationToken cancellationToken);
    Task<VSInternalHover?> HandleDelegatedResponseAsync(VSInternalHover? response, VersionedDocumentContext versionedDocumentContext, DocumentPositionInfo positionInfo, IRazorDocumentMappingService documentMappingService, CancellationToken cancellationToken);
}
