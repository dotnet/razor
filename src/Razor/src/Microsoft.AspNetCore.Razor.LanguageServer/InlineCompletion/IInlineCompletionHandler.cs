// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MediatR;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class VSInternalInlineCompletionRequestBridge : VSInternalInlineCompletionRequest, IRequest<VSInternalInlineCompletionList?>
{
}

[Parallel, Method("textDocument/_vs_inlineCompletion")]
internal interface IInlineCompletionHandler : IJsonRpcRequestHandler<VSInternalInlineCompletionRequestBridge, VSInternalInlineCompletionList?>, IRegistrationExtension
{
}
