// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    [Parallel, Method(TextDocumentNames.CodeActionResolve, Direction.ClientToServer)]
    internal interface IRazorCodeActionResolveHandler :
        IJsonRpcRequestHandler<CodeAction, CodeAction>
    {
    }
}
