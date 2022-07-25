// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Test;
using Microsoft.AspNetCore.Razor.Test.Common;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal class TestRazorFormattingService
    {
        public static readonly RazorFormattingService Instance = CreateWithFullSupport(TestRazorCodeDocument.CreateEmpty());

        private TestRazorFormattingService()
        {
        }

        public static RazorFormattingService CreateWithFullSupport(RazorCodeDocument codeDocument)
        {
            var mappingService = new DefaultRazorDocumentMappingService(TestLoggerFactory.Instance);

            var dispatcher = new LSPProjectSnapshotManagerDispatcher(TestLoggerFactory.Instance);
            var versionCache = new DefaultDocumentVersionCache(dispatcher);

            var client = new FormattingLanguageServerClient();
            client.AddCodeDocument(codeDocument);

            var passes = new List<IFormattingPass>()
            {
                new HtmlFormattingPass(mappingService, FilePathNormalizer.Instance, client, versionCache, TestLoggerFactory.Instance),
                new CSharpFormattingPass(mappingService, FilePathNormalizer.Instance, client, TestLoggerFactory.Instance),
                new CSharpOnTypeFormattingPass(mappingService, FilePathNormalizer.Instance, client, TestLoggerFactory.Instance),
                new RazorFormattingPass(mappingService, FilePathNormalizer.Instance, client, TestLoggerFactory.Instance),
                new FormattingDiagnosticValidationPass(mappingService, FilePathNormalizer.Instance, client, TestLoggerFactory.Instance),
                new FormattingContentValidationPass(mappingService, FilePathNormalizer.Instance, client, TestLoggerFactory.Instance),
            };

            return new DefaultRazorFormattingService(passes, TestLoggerFactory.Instance, TestAdhocWorkspaceFactory.Instance);
        }
    }
}
