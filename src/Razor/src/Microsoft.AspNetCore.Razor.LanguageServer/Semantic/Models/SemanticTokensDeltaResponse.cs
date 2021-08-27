// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.Serialization;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models
{
    /// <summary>
    /// Roslyn supports frozen compilations and may only send us back partial tokens
    /// until the full compilation is available. Razor needs to know if the tokens
    /// are incomplete so we can continue to queue Roslyn for full tokens.
    /// </summary>
    internal record SemanticTokensDeltaResponse : SemanticTokensDelta
    {
        [DataMember(Name = "isPartial")]
        public bool IsPartial { get; set; }
    }
}
