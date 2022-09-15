// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts
{
    // TODO: Should be an abstract class that says if it's serial
    [LanguageServerEndpoint(Methods.TextDocumentDidCloseName)]
    internal interface IVSDidCloseTextDocumentEndpoint: IRazorNotificationHandler<DidCloseTextDocumentParams>,
        ITextDocumentIdentifierHandler<DidCloseTextDocumentParams, TextDocumentIdentifier>
    {
    }
}
