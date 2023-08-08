﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentPresentation;

[LanguageServerEndpoint(VSInternalMethods.TextDocumentTextPresentationName)]
internal interface ITextDocumentTextPresentationHandler : IRazorRequestHandler<TextPresentationParams, WorkspaceEdit?>, ICapabilitiesProvider
{
}
