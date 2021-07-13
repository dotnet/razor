// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    [Parallel, Method("codeAction/resolve", Direction.ClientToServer)]
    internal interface IRazorCodeActionResolveHandler :
        IJsonRpcRequestHandler<CodeAction, CodeAction>
    {
    }
}
