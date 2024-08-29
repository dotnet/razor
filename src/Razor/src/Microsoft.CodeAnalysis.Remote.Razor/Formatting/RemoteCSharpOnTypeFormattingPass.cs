﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting;

[Export(typeof(IFormattingPass)), Shared]
[method: ImportingConstructor]
internal sealed class RemoteCSharpOnTypeFormattingPass(
    IDocumentMappingService documentMappingService,
    ILoggerFactory loggerFactory)
    : CSharpOnTypeFormattingPassBase(documentMappingService, loggerFactory)
{
    protected override Task<TextEdit[]> AddUsingStatementEditsIfNecessaryAsync(CodeAnalysis.Razor.Formatting.FormattingContext context, RazorCodeDocument codeDocument, SourceText csharpText, TextEdit[] textEdits, SourceText originalTextWithChanges, TextEdit[] finalEdits, CancellationToken cancellationToken)
    {
        // Implement this when code actions are migrated to cohosting,
        // probably will be able to move it back into base class and make that non-abstract.

        return Task.FromResult(finalEdits);
    }
}
