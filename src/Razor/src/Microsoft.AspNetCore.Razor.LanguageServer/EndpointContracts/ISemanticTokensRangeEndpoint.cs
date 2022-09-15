// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CommonLanguageServerProtocol.Framework;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts
{
    [LanguageServerEndpoint(Methods.TextDocumentSemanticTokensRangeName)]
    internal interface ISemanticTokensRangeEndpoint : IRazorRequestHandler<SemanticTokensRangeParamsBridge, SemanticTokens?>,
        IRegistrationExtension
    {
    }
}
