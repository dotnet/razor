// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MediatR;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal class CodeActionBridge : CodeAction, IRequest<CodeAction?>
    {
    }

    [Parallel, Method(Methods.CodeActionResolveName, Direction.ClientToServer)]
    internal interface IRazorCodeActionResolveHandler :
        IJsonRpcRequestHandler<CodeActionBridge, CodeAction?>
    {
    }
}
