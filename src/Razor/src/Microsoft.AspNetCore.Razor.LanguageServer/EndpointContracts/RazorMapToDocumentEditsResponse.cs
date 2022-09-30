// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts
{
    internal class RazorMapToDocumentEditsResponse
    {
        public required TextEdit[] TextEdits { get; init; }

        public int? HostDocumentVersion { get; init; }
    }
}
