// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer.LinkedEditingRange
{
    [Parallel, Method("textDocument/linkedEditingRange")]
    internal interface ILinkedEditingRangeHandler : IJsonRpcRequestHandler<LinkedEditingRangeParams, LinkedEditingRanges?>, IRegistrationExtension
    {
    }
}
