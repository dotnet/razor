// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.
#pragma warning disable CS0618
#nullable enable
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class SemanticTokensLegendParams : IRequest<SemanticTokensLegend>, IBaseRequest
    {
    }
}
