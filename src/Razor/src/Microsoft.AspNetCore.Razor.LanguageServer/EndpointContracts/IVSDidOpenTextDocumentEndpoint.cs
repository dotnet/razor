﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts
{
    [LanguageServerEndpoint(Methods.TextDocumentDidOpenName)]
    internal interface IVSDidOpenTextDocumentEndpoint : IRazorNotificationHandler<DidOpenTextDocumentParams>,
        ITextDocumentIdentifierHandler<DidOpenTextDocumentParams, Uri>
    {
    }
}
