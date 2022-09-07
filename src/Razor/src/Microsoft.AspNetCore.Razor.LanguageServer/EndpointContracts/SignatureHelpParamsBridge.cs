// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MediatR;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using LS = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;

internal class SignatureHelpParamsBridge : TextDocumentPositionParams, IRequest<LS.SignatureHelp?>, ITextDocumentPositionParams
{
}
