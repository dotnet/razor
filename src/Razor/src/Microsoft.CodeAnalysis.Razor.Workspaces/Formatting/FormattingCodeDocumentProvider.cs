// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.Formatting;

internal sealed class FormattingCodeDocumentProvider : IFormattingCodeDocumentProvider
{
    public Task<RazorCodeDocument> GetCodeDocumentAsync(IDocumentSnapshot snapshot)
    {
        var useDesignTimeGeneratedOutput = snapshot.Project.Configuration.LanguageServerFlags?.ForceRuntimeCodeGeneration ?? false;
        return snapshot.GetGeneratedOutputAsync(useDesignTimeGeneratedOutput);
    }
}
