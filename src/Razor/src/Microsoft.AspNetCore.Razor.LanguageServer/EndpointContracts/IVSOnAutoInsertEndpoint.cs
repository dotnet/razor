// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer.AutoInsert
{
    [Parallel, Method(VSInternalMethods.OnAutoInsertName)]
    internal interface IVSOnAutoInsertEndpoint : IJsonRpcRequestHandler<OnAutoInsertParamsBridge, VSInternalDocumentOnAutoInsertResponseItem?>, IRegistrationExtension
    {
    }
}
