// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

[Parallel, Method("textDocument/_vs_inlineCompletion")]
internal interface IInlineCompletionHandler : IJsonRpcRequestHandler<InlineCompletionRequest, InlineCompletionList?>, IRegistrationExtension
{
}
