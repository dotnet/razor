// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test
{
    internal class TestDocumentContextFactory : DocumentContextFactory
    {
        private readonly string? _filePath;
        private readonly RazorCodeDocument? _codeDocument;

        public TestDocumentContextFactory()
        {
        }

        public TestDocumentContextFactory(string filePath, RazorCodeDocument codeDocument)
        {
            _filePath = filePath;
            _codeDocument = codeDocument;
        }

        public override Task<DocumentContext?> TryCreateAsync(Uri documentUri, CancellationToken cancellationToken)
        {
            if (_filePath is null || _codeDocument is null)
            {
                return Task.FromResult<DocumentContext?>(null);
            }

            var documentContext = TestDocumentContext.From(_filePath, _codeDocument);
            return Task.FromResult<DocumentContext?>(documentContext);
        }
    }
}
