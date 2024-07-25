// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using RLSP = Roslyn.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Extensions;

internal static class TextDocumentExtensions
{
    public static RazorTextDocumentIdentifier ToRazorTextDocumentIdentifier(this TextDocumentIdentifier textDocumentIdentifier)
        => new RazorTextDocumentIdentifier(textDocumentIdentifier.Uri, (textDocumentIdentifier as VSTextDocumentIdentifier)?.ProjectContext?.Id);

    public static RazorTextDocumentIdentifier ToRazorTextDocumentIdentifier(this RLSP.TextDocumentIdentifier textDocumentIdentifier)
        => new RazorTextDocumentIdentifier(textDocumentIdentifier.Uri, (textDocumentIdentifier as RLSP.VSTextDocumentIdentifier)?.ProjectContext?.Id);
}
