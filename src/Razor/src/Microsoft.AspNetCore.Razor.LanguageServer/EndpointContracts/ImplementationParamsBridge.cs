// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MediatR;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using ImplementationResult = Microsoft.VisualStudio.LanguageServer.Protocol.SumType<
    Microsoft.VisualStudio.LanguageServer.Protocol.Location[]?,
    Microsoft.VisualStudio.LanguageServer.Protocol.VSInternalReferenceItem[]?>;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts
{
    internal class ImplementationParamsBridge : TextDocumentPositionParams, IRequest<ImplementationResult>
    {
    }
}
