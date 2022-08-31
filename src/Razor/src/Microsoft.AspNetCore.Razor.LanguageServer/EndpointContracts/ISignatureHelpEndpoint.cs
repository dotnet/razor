// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.LanguageServer.Protocol;
using OmniSharp.Extensions.JsonRpc;
using LS = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts
{
    [Parallel, Method(Methods.TextDocumentSignatureHelpName)]
    internal interface ISignatureHelpEndpoint : IJsonRpcRequestHandler<SignatureHelpParamsBridge, LS.SignatureHelp?>,
        IRegistrationExtension
    {
    }
}
