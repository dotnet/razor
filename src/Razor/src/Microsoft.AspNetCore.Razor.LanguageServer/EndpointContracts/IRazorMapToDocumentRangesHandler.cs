// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using System;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

[LanguageServerEndpoint(LanguageServerConstants.RazorMapToDocumentRangesEndpoint)]
internal interface IRazorMapToDocumentRangesHandler :
    IRazorDocumentlessRequestHandler<RazorMapToDocumentRangesParams, RazorMapToDocumentRangesResponse?>,
    ITextDocumentIdentifierHandler<RazorMapToDocumentRangesParams, Uri>
{
}
