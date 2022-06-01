// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using OmniSharp.Extensions.JsonRpc;
using DefinitionResult = Microsoft.VisualStudio.LanguageServer.Protocol.SumType<
    Microsoft.VisualStudio.LanguageServer.Protocol.VSInternalLocation,
    Microsoft.VisualStudio.LanguageServer.Protocol.VSInternalLocation[],
    Microsoft.VisualStudio.LanguageServer.Protocol.DocumentLink[]>;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts
{
    internal interface IDefinitionEndpoint : IJsonRpcRequestHandler<DefinitionParams, DefinitionResult?>,
        IRegistrationExtension
    {
    }
}
