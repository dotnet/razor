// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.GoToDefinition;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.CodeAnalysis.Remote.Razor.GoToDefinition;

[Export(typeof(IRazorComponentDefinitionService)), Shared]
[method: ImportingConstructor]
internal sealed class RazorComponentDefinitionService(
    IRazorComponentSearchEngine componentSearchEngine,
    IDocumentMappingService documentMappingService,
    ITagHelperSearchEngine tagHelperSearchEngine,
    ILoggerFactory loggerFactory)
    : AbstractRazorComponentDefinitionService(componentSearchEngine, tagHelperSearchEngine, documentMappingService, loggerFactory.GetOrCreateLogger<RazorComponentDefinitionService>())
{
}
