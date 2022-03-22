// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    /// <summary>
    /// Resolves and remaps the code action, without running formatting passes.
    /// </summary>
    internal class UnformattedRemappingCSharpCodeActionResolver : CSharpCodeActionResolver
    {
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly DocumentVersionCache _documentVersionCache;
        private readonly RazorDocumentMappingService _documentMappingService;

        public UnformattedRemappingCSharpCodeActionResolver(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher!!,
            DocumentResolver documentResolver!!,
            ClientNotifierServiceBase languageServer,
            DocumentVersionCache documentVersionCache!!,
            RazorDocumentMappingService documentMappingService!!)
            : base(languageServer)
        {
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _documentResolver = documentResolver;
            _documentVersionCache = documentVersionCache;
            _documentMappingService = documentMappingService;
        }

        public override string Action => LanguageServerConstants.CodeActions.UnformattedRemap;

        public async override Task<CodeAction?> ResolveAsync(
            CSharpCodeActionParams csharpParams!!,
            CodeAction codeAction!!,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resolvedCodeAction = await ResolveCodeActionWithServerAsync(csharpParams.RazorFileUri, codeAction, cancellationToken).ConfigureAwait(false);
            if (resolvedCodeAction?.Edit?.DocumentChanges is null)
            {
                // Unable to resolve code action with server, return original code action
                return codeAction;
            }

            if (resolvedCodeAction.Edit.DocumentChanges.Count() != 1)
            {
                // We don't yet support multi-document code actions, return original code action
                Debug.Fail($"Encountered an unsupported multi-document code action edit with ${codeAction.Title}.");
                return codeAction;
            }

            var documentChanged = resolvedCodeAction.Edit.DocumentChanges.First();
            if (!documentChanged.IsTextDocumentEdit)
            {
                // Only Text Document Edit changes are supported currently, return original code action
                return codeAction;
            }

            var textEdit = documentChanged.TextDocumentEdit!.Edits.FirstOrDefault();
            if (textEdit is null)
            {
                // No text edit available
                return codeAction;
            }

            var documentInfo = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync<(DocumentSnapshot, int)?>(() =>
            {
                if (_documentResolver.TryResolveDocument(csharpParams.RazorFileUri.ToUri().GetAbsoluteOrUNCPath(), out var documentSnapshot))
                {
                    if (_documentVersionCache.TryGetDocumentVersion(documentSnapshot, out var version))
                    {
                        return (documentSnapshot, version.Value);
                    }
                }

                return null;
            }, cancellationToken).ConfigureAwait(false);
            if (documentInfo is null)
            {
                return codeAction;
            }

            var (documentSnapshot, documentVersion) = documentInfo.Value;

            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
            if (codeDocument.IsUnsupported())
            {
                return codeAction;
            }

            if (!_documentMappingService.TryMapFromProjectedDocumentRange(codeDocument, textEdit.Range, MappingBehavior.Inclusive, out var originalRange))
            {
                // Text edit failed to map
                return codeAction;
            }

            textEdit = textEdit with { Range = originalRange };

            var codeDocumentIdentifier = new OptionalVersionedTextDocumentIdentifier()
            {
                Uri = csharpParams.RazorFileUri,
                Version = documentVersion
            };

            resolvedCodeAction = resolvedCodeAction with
            {
                Edit = new WorkspaceEdit()
                {
                    DocumentChanges = new[] {
                    new WorkspaceEditDocumentChange(
                        new TextDocumentEdit()
                        {
                            TextDocument = codeDocumentIdentifier,
                            Edits = new[] { textEdit },
                        })
                }
                },
            };

            return resolvedCodeAction;
        }
    }
}
