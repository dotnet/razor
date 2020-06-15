// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using MediatR;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Interfaces
{
    public class SemanticTokensEditParams : IRequest<SemanticTokensOrSemanticTokensEdits?>
    {
        public RazorLanguageKind Kind { get; set; }
        public Uri RazorDocumentUri { get; set; }
        public string PreviousResultId { get; set; }
    }

    public class SemanticTokensEditCapability : DynamicCapability
    {
    }

    [Serial, Method(LanguageServerConstants.RazorSemanticTokensEditEndpoint)]
    internal interface ISemanticTokenEditHandler :
        IJsonRpcRequestHandler<SemanticTokensEditParams, SemanticTokensOrSemanticTokensEdits?>,
        IRequestHandler<SemanticTokensEditParams, SemanticTokensOrSemanticTokensEdits?>,
        IJsonRpcHandler,
        ICapability<SemanticTokensEditCapability>
    {
    }
}
