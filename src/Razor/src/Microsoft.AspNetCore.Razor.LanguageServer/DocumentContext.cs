// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal record DocumentContext
    {
        private RazorCodeDocument? _codeDocument;
        private SourceText? _sourceText;

        public DocumentContext(
            Uri uri,
            DocumentSnapshot snapshot,
            int version)
        {
            Uri = uri;
            Snapshot = snapshot;
            Version = version;
        }

        public Uri Uri { get; }

        public DocumentSnapshot Snapshot { get; }

        public int Version { get; }

        public string FilePath => Snapshot.FilePath;

        public string FileKind => Snapshot.FileKind;

        public ProjectSnapshot Project => Snapshot.Project;

        public VersionedTextDocumentIdentifier Identifier => new VersionedTextDocumentIdentifier()
        {
            Uri = Uri,
            Version = Version,
        };

        public async Task<RazorCodeDocument> GetCodeDocumentAsync(CancellationToken cancellationToken)
        {
            if (_codeDocument is null)
            {
                var codeDocument = await Snapshot.GetGeneratedOutputAsync().ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                _codeDocument = codeDocument;
            }

            return _codeDocument;
        }

        public async Task<SourceText> GetSourceTextAsync(CancellationToken cancellationToken)
        {
            if (_sourceText is null)
            {
                var sourceText = await Snapshot.GetTextAsync().ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                _sourceText = sourceText;
            }

            return _sourceText;
        }

        public async Task<RazorSyntaxTree> GetSyntaxTreeAsync(CancellationToken cancellationToken)
        {
            var codeDocument = await GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
            var syntaxTree = codeDocument.GetSyntaxTree();

            return syntaxTree;
        }

        public async Task<TagHelperDocumentContext> GetTagHelperContextAsync(CancellationToken cancellationToken)
        {
            var codeDocument = await GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
            var tagHelperContext = codeDocument.GetTagHelperContext();

            return tagHelperContext;
        }

        public async Task<SourceText> GetCSharpSourceTextAsync(CancellationToken cancellationToken)
        {
            var codeDocument = await GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
            var sourceText = codeDocument.GetCSharpSourceText();

            return sourceText;
        }

        public async Task<SourceText> GetHtmlSourceTextAsync(CancellationToken cancellationToken)
        {
            var codeDocument = await GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
            var sourceText = codeDocument.GetHtmlSourceText();

            return sourceText;
        }
    }
}
