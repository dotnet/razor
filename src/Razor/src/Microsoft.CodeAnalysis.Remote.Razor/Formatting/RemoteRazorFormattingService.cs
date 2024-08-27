// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Composition;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.CodeAnalysis.Remote.Razor.Formatting;

[Export(typeof(IRazorFormattingService)), Shared]
[method: ImportingConstructor]
internal class RemoteRazorFormattingService(
    [ImportMany] IEnumerable<IFormattingPass> formattingPasses,
    IFormattingCodeDocumentProvider codeDocumentProvider,
    IAdhocWorkspaceFactory adhocWorkspaceFactory)
    : RazorFormattingService(
        formattingPasses,
        codeDocumentProvider,
        adhocWorkspaceFactory)
{
}
