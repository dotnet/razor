// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class RazorLanguageQueryResponse
    {
        public RazorLanguageKind Kind { get; set; }

        public int PositionIndex { get; set; }

        public required Position Position { get; set; }

        public int? HostDocumentVersion { get; set; }
    }
}
