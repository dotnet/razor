// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.GoToDefinition;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Definition;

internal sealed class RazorComponentDefinitionService(
    IRazorComponentSearchEngine componentSearchEngine,
    IDocumentMappingService documentMappingService,
    ILoggerFactory loggerFactory)
    : AbstractRazorComponentDefinitionService(componentSearchEngine, documentMappingService, loggerFactory.GetOrCreateLogger<RazorComponentDefinitionService>())
{
    protected override async ValueTask<SyntaxTree> GetCSharpSyntaxTreeAsync(IDocumentSnapshot documentSnapshot, CancellationToken cancellationToken)
    {
        var codeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
        var csharpText = codeDocument.GetCSharpSourceText();
        return CSharpSyntaxTree.ParseText(csharpText, cancellationToken: cancellationToken);
    }
}
