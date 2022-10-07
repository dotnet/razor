// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Test;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal class TestRazorFormattingService
    {
        private TestRazorFormattingService()
        {
        }

        public static RazorFormattingService CreateWithFullSupport(RazorCodeDocument? codeDocument = null, ILoggerFactory? loggerFactory = null)
        {
            codeDocument ??= TestRazorCodeDocument.CreateEmpty();
            loggerFactory ??= NullLoggerFactory.Instance;

            var mappingService = new DefaultRazorDocumentMappingService(TestLanguageServerFeatureOptions.Instance, new TestDocumentContextFactory(), loggerFactory);

            var dispatcher = new LSPProjectSnapshotManagerDispatcher(loggerFactory);
            var versionCache = new DefaultDocumentVersionCache(dispatcher);

            var client = new FormattingLanguageServerClient();
            client.AddCodeDocument(codeDocument);

            var passes = new List<IFormattingPass>()
            {
                new HtmlFormattingPass(mappingService, FilePathNormalizer.Instance, client, versionCache, loggerFactory),
                new CSharpFormattingPass(mappingService, FilePathNormalizer.Instance, client, loggerFactory),
                new CSharpOnTypeFormattingPass(mappingService, FilePathNormalizer.Instance, client, loggerFactory),
                new RazorFormattingPass(mappingService, FilePathNormalizer.Instance, client, loggerFactory),
                new FormattingDiagnosticValidationPass(mappingService, FilePathNormalizer.Instance, client, loggerFactory),
                new FormattingContentValidationPass(mappingService, FilePathNormalizer.Instance, client, loggerFactory),
            };

            return new DefaultRazorFormattingService(passes, loggerFactory, TestAdhocWorkspaceFactory.Instance);
        }
    }
}
