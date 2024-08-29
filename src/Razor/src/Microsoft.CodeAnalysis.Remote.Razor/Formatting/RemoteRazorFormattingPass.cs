// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Formatting;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting;

[Export(typeof(IFormattingPass)), Shared]
[method: ImportingConstructor]
internal sealed class RemoteRazorFormattingPass(
    IDocumentMappingService documentMappingService)
    : RazorFormattingPassBase(documentMappingService)
{
    // TODO: properly plumb this through
    protected override bool CodeBlockBraceOnNextLine => false;
}
