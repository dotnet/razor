// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.Serialization;
using Microsoft.AspNetCore.Razor.LanguageServer.Folding;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts
{
    internal class RazorFoldingRangeRequestParam : VSFoldingRangeParamsBridge
    {
        [DataMember(Name = "hostDocumentVersion")]
        public int HostDocumentVersion { get; init; }
    }
}
