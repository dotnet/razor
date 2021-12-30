// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    internal class RazorDocumentRangeFormattingResponse
    {
#pragma warning disable CA1819 // Properties should not return arrays
        public TextEdit[] Edits { get; set; }
#pragma warning restore CA1819 // Properties should not return arrays
    }
}
