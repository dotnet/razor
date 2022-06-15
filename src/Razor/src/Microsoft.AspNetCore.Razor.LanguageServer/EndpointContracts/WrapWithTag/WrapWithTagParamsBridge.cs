// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MediatR;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts.WrapWithTag;

internal class WrapWithTagParamsBridge : WrapWithTagParams, IRequest<WrapWithTagResponse>
{
    public WrapWithTagParamsBridge(TextDocumentIdentifier textDocument) : base(textDocument)
    {
    }
}
