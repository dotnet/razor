// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.CodeAnalysis.Remote.Razor.DocumentMapping;

[Export(typeof(IRazorDocumentMappingService)), Shared]
[method: ImportingConstructor]
internal sealed class RemoteDocumentMappingService(
    LanguageServerFeatureOptions languageServerFeatureOptions,
    IDocumentContextFactory documentContextFactory)
    : AbstractRazorDocumentMappingService(
        ConstructFilePathService(languageServerFeatureOptions), // TODO: Fix FilePathService
        documentContextFactory,
        NullLogger.Instance) // TODO: Logging
{
    private static FilePathService ConstructFilePathService(LanguageServerFeatureOptions languageServerFeatureOptions)
    {
        // Can't put FilePathService in the MEF catalog because its in Workspaces
        return new FilePathService(languageServerFeatureOptions);
    }
}
