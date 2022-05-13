// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MediatR;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using VSSemanticTokensRangeParams = Microsoft.VisualStudio.LanguageServer.Protocol.SemanticTokensRangeParams;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts
{
    internal class SemanticTokensRangeParamsBridge : VSSemanticTokensRangeParams, IRequest<SemanticTokens?>
    { }
}
