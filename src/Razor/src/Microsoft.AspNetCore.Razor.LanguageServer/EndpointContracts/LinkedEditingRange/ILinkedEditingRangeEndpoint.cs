// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts.LinkedEditingRange
{
    [Parallel, Method(Methods.TextDocumentLinkedEditingRangeName)]
    internal interface ILinkedEditingRangeEndpoint : IJsonRpcRequestHandler<LinkedEditingRangeParamsBridge, LinkedEditingRanges?>, IRegistrationExtension
    {
    }
}
