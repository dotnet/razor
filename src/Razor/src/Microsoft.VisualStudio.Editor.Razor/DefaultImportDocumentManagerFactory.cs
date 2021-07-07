// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Editor.Razor.Documents;

namespace Microsoft.VisualStudio.Editor.Razor
{
    [Shared]
    [ExportLanguageServiceFactory(typeof(ImportDocumentManager), RazorLanguage.Name, ServiceLayer.Default)]
    internal class DefaultImportDocumentManagerFactory : ILanguageServiceFactory
    {
        private readonly SingleThreadedDispatcher _singleThreadedDispatcher;

        [ImportingConstructor]
        public DefaultImportDocumentManagerFactory(SingleThreadedDispatcher singleThreadedDispatcher)
        {
            if (singleThreadedDispatcher == null)
            {
                throw new ArgumentNullException(nameof(singleThreadedDispatcher));
            }

            _singleThreadedDispatcher = singleThreadedDispatcher;
        }

        public ILanguageService CreateLanguageService(HostLanguageServices languageServices)
        {
            if (languageServices == null)
            {
                throw new ArgumentNullException(nameof(languageServices));
            }

            var errorReporter = languageServices.WorkspaceServices.GetRequiredService<ErrorReporter>();
            var fileChangeTrackerFactory = languageServices.WorkspaceServices.GetRequiredService<FileChangeTrackerFactory>();

            return new DefaultImportDocumentManager(_singleThreadedDispatcher, errorReporter, fileChangeTrackerFactory);
        }
    }
}
