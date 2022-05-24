// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal class TestLSPDocumentContextFactory : DocumentContextFactory
    {
        private readonly string? _filePath;
        private readonly RazorCodeDocument? _codeDocument;

        public TestLSPDocumentContextFactory()
        {
        }

        public TestLSPDocumentContextFactory(string filePath, RazorCodeDocument codeDocument)
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

            var content = _codeDocument.GetSourceText().ToString();
            var documentSnapshot = TestDocumentSnapshot.Create(_filePath, content);
            documentSnapshot.With(_codeDocument);
            return Task.FromResult<DocumentContext?>(new DocumentContext(documentUri, documentSnapshot, version: 0));
        }
    }
}
