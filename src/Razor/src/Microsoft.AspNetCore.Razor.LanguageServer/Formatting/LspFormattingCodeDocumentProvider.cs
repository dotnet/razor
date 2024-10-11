// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting;

internal sealed class LspFormattingCodeDocumentProvider : IFormattingCodeDocumentProvider
{
    public ValueTask<RazorCodeDocument> GetCodeDocumentAsync(IDocumentSnapshot snapshot, CancellationToken cancellationToken)
    {
        // Formatting always uses design time
        return snapshot.GetGeneratedOutputAsync(forceDesignTimeGeneratedOutput: true, cancellationToken);
    }
}
