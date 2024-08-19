// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.GoToDefinition;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Definition;

internal sealed class RazorComponentDefinitionService(
    IRazorComponentSearchEngine componentSearchEngine,
    IDocumentMappingService documentMappingService,
    ILoggerFactory loggerFactory)
    : AbstractRazorComponentDefinitionService(componentSearchEngine, documentMappingService, loggerFactory.GetOrCreateLogger<RazorComponentDefinitionService>())
{
}
