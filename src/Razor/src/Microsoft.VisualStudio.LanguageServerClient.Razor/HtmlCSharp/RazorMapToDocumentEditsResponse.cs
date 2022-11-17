﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp;

// Note: This type should be kept in sync with the one in Razor.LanguageServer assembly.
internal class RazorMapToDocumentEditsResponse
{
    public required TextEdit[] TextEdits { get; init; }

    public long HostDocumentVersion { get; init; }
}
