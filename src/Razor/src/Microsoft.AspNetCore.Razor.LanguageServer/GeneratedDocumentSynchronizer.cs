// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class GeneratedDocumentSynchronizer(
    IGeneratedDocumentPublisher publisher,
    LanguageServerFeatureOptions languageServerFeatureOptions) : IDocumentProcessedListener
{
    private readonly IGeneratedDocumentPublisher _publisher = publisher;
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions = languageServerFeatureOptions;

    public void DocumentProcessed(RazorCodeDocument codeDocument, IDocumentSnapshot document)
    {
        var hostDocumentVersion = document.Version;

        var filePath = document.FilePath.AssumeNotNull();

        // If cohosting is on, then it is responsible for updating the Html buffer
        if (!_languageServerFeatureOptions.UseRazorCohostServer)
        {
            var htmlText = codeDocument.GetHtmlSourceText();

            _publisher.PublishHtml(document.Project.Key, filePath, htmlText, hostDocumentVersion);
        }

        var csharpText = codeDocument.GetCSharpSourceText();

        _publisher.PublishCSharp(document.Project.Key, filePath, csharpText, hostDocumentVersion);
    }
}
