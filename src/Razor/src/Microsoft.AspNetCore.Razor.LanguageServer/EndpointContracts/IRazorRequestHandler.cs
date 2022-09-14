// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts
{
    internal interface IRazorRequestHandler<TRequestType, TResponseType> : IRequestHandler<TRequestType, TResponseType, RazorRequestContext>, ITextDocumentIdentifierHandler<TRequestType, TextDocumentIdentifier>
    {
    }

    internal interface IRazorDocumentlessRequestHandler<TRequestType, TResponseType> : IRequestHandler<TRequestType, TResponseType, RazorRequestContext>
    {
    }
}
