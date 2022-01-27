// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Runtime.Serialization;
using MediatR;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

/// <summary>
/// Corresponds to https://devdiv.visualstudio.com/DevDiv/_git/VSLanguageServerClient?path=/src/product/Protocol/LanguageServer.Protocol.Internal/VSInternalInlineCompletionRequest.cs
/// </summary>
public class InlineCompletionRequest : ITextDocumentIdentifierParams, IRequest<InlineCompletionList>, IBaseRequest
{
    [DataMember(Name = "_vs_textDocument")]
    [JsonProperty(Required = Required.Always)]
    public TextDocumentIdentifier TextDocument { get; set; }

    [DataMember(Name = "_vs_position")]
    [JsonProperty(Required = Required.Always)]
    public Position Position { get; set; }

    [DataMember(Name = "_vs_context")]
    [JsonProperty(Required = Required.Always)]
    public InlineCompletionContext Context { get; set; }
}
