// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultDocumentContextFactory : DocumentContextFactory
    {
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly DocumentVersionCache _documentVersionCache;
        private readonly ILogger<DefaultDocumentContextFactory> _logger;

        public DefaultDocumentContextFactory(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentResolver documentResolver,
            DocumentVersionCache documentVersionCache,
            ILoggerFactory loggerFactory)
        {
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _documentResolver = documentResolver;
            _documentVersionCache = documentVersionCache;
            _logger = loggerFactory.CreateLogger<DefaultDocumentContextFactory>();
        }

        public override async Task<DocumentContext?> TryCreateAsync(Uri documentUri, CancellationToken cancellationToken)
        {
            var filePath = documentUri.GetAbsoluteOrUNCPath();
            var documentAndVersion = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                if (_documentResolver.TryResolveDocument(filePath, out var documentSnapshot))
                {
                    if (_documentVersionCache.TryGetDocumentVersion(documentSnapshot, out var version))
                    {
                        return new DocumentSnapshotAndVersion(documentSnapshot, version.Value);
                    }
                }

                return null;
            }, cancellationToken).ConfigureAwait(false);

            if (documentAndVersion is null)
            {
                return null;
            }

            var (documentSnapshot, version) = documentAndVersion;
            if (documentSnapshot is null)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
            if (codeDocument.IsUnsupported())
            {
                _logger.LogWarning($"Failed to retrieve generated output for document {documentUri}. It is unsupported.");
                return null;
            }

            var sourceText = await documentSnapshot.GetTextAsync().ConfigureAwait(false);
            if (sourceText is null)
            {
                return null;
            }

            var context = new DocumentContext(documentUri, codeDocument, documentSnapshot, sourceText, version);
            return context;
        }

        private record DocumentSnapshotAndVersion(DocumentSnapshot Snapshot, int Version);
    }
}
