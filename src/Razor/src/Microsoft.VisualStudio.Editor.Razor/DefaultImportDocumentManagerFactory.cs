// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Editor.Razor.Documents;

namespace Microsoft.VisualStudio.Editor.Razor;

[Shared]
[ExportLanguageServiceFactory(typeof(IImportDocumentManager), RazorLanguage.Name, ServiceLayer.Default)]
internal class DefaultImportDocumentManagerFactory : ILanguageServiceFactory
{
    private readonly ProjectSnapshotManagerDispatcher _dispatcher;

    [ImportingConstructor]
    public DefaultImportDocumentManagerFactory(ProjectSnapshotManagerDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public ILanguageService CreateLanguageService(HostLanguageServices languageServices)
    {
        if (languageServices is null)
        {
            throw new ArgumentNullException(nameof(languageServices));
        }

        var fileChangeTrackerFactory = languageServices.WorkspaceServices.GetRequiredService<FileChangeTrackerFactory>();

        return new ImportDocumentManager(_dispatcher, fileChangeTrackerFactory);
    }
}
