// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Composition;
using Microsoft.CodeAnalysis.Razor.CodeActions;
using Microsoft.CodeAnalysis.Razor.CodeActions.Razor;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.CodeAnalysis.Remote.Razor.CodeActions;

[Export(typeof(ICodeActionsService)), Shared]
[method: ImportingConstructor]
internal sealed class OOPCodeActionsService(
    IDocumentMappingService documentMappingService,
    [ImportMany] IEnumerable<IRazorCodeActionProvider> razorCodeActionProviders,
    [ImportMany] IEnumerable<ICSharpCodeActionProvider> csharpCodeActionProviders,
    [ImportMany] IEnumerable<IHtmlCodeActionProvider> htmlCodeActionProviders,
    LanguageServerFeatureOptions languageServerFeatureOptions)
    : CodeActionsService(documentMappingService, razorCodeActionProviders, csharpCodeActionProviders, htmlCodeActionProviders, languageServerFeatureOptions);

[Export(typeof(IRazorCodeActionProvider)), Shared]
[method: ImportingConstructor]
internal sealed class OOPExtractToCodeBehindCodeActionProvider(ILoggerFactory loggerFactory) : ExtractToCodeBehindCodeActionProvider(loggerFactory);

[Export(typeof(IRazorCodeActionProvider)), Shared]
internal sealed class OOPExtractToComponentCodeActionProvider : ExtractToComponentCodeActionProvider;

[Export(typeof(IRazorCodeActionProvider)), Shared]
internal sealed class OOPComponentAccessibilityCodeActionProvider : ComponentAccessibilityCodeActionProvider;

[Export(typeof(IRazorCodeActionProvider)), Shared]
internal sealed class OOPGenerateMethodCodeActionProvider : GenerateMethodCodeActionProvider;

[Export(typeof(ICSharpCodeActionProvider)), Shared]
internal sealed class OOPTypeAccessibilityCodeActionProvider : TypeAccessibilityCodeActionProvider;

[Export(typeof(ICSharpCodeActionProvider)), Shared]
[method: ImportingConstructor]
internal sealed class OOPDefaultCSharpCodeActionProvider(LanguageServerFeatureOptions languageServerFeatureOptions) : CSharpCodeActionProvider(languageServerFeatureOptions);

[Export(typeof(IHtmlCodeActionProvider)), Shared]
[method: ImportingConstructor]
internal sealed class OOPDefaultHtmlCodeActionProvider(IEditMappingService editMappingService) : HtmlCodeActionProvider(editMappingService);
