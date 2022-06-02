// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using OmniSharp.Extensions.JsonRpc;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentPresentation
{
    [Parallel, Method(VSInternalMethods.TextDocumentTextPresentationName)]
    internal interface ITextDocumentTextPresentationHandler : IJsonRpcRequestHandler<TextPresentationParams, WorkspaceEdit?>, IRegistrationExtension
    {
    }
}
