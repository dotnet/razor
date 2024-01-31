// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.InlayHints;

internal class RazorInlayHintWrapper
{
    public required TextDocumentIdentifier TextDocument { get; set; }
    public required object? OriginalData { get; set; }
    public required Position OriginalPosition { get; set; }
}
