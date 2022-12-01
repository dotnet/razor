// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.LanguageServer.Protocol;
using ITextDocumentPositionParams = Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts.ITextDocumentPositionParams;

namespace Microsoft.AspNetCore.Razor.LanguageServer.SignatureHelp;

internal class SignatureHelpParamsBridge : SignatureHelpParams, ITextDocumentPositionParams //
{
}
