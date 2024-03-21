// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.SemanticTokens;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.CodeAnalysis.Remote.Razor.SemanticTokens;

[Export(typeof(IRazorSemanticTokensInfoService)), Shared]
[method: ImportingConstructor]
internal class RazorSemanticTokensInfoService(
    IRazorDocumentMappingService documentMappingService,
    ISemanticTokensLegendService semanticTokensLegendService,
    ICSharpSemanticTokensProvider csharpSemanticTokensProvider,
    LanguageServerFeatureOptions languageServerFeatureOptions)
    : AbstractRazorSemanticTokensInfoService(
        documentMappingService,
        semanticTokensLegendService,
        csharpSemanticTokensProvider,
        languageServerFeatureOptions,
        NullLogger.Instance) // TODO: Logging
{
}
