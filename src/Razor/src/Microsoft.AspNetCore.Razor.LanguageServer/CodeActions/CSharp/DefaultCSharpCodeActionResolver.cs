﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal class DefaultCSharpCodeActionResolver : CSharpCodeActionResolver
    {
        // Usually when we need to format code, we utilize the formatting options provided
        // by the platform. However, we aren't provided such options in the case of code actions
        // so we use a default (and commonly used) configuration.
        private static readonly FormattingOptions s_defaultFormattingOptions = new FormattingOptions()
        {
            TabSize = 4,
            InsertSpaces = true,
            TrimTrailingWhitespace = true,
            InsertFinalNewline = true,
            TrimFinalNewlines = true
        };

        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly RazorFormattingService _razorFormattingService;
        private readonly DocumentVersionCache _documentVersionCache;

        public DefaultCSharpCodeActionResolver(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentResolver documentResolver,
            ClientNotifierServiceBase languageServer,
            RazorFormattingService razorFormattingService,
            DocumentVersionCache documentVersionCache)
            : base(languageServer)
        {
            if (projectSnapshotManagerDispatcher is null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            }

            if (documentResolver is null)
            {
                throw new ArgumentNullException(nameof(documentResolver));
            }

            if (razorFormattingService is null)
            {
                throw new ArgumentNullException(nameof(razorFormattingService));
            }

            if (documentVersionCache is null)
            {
                throw new ArgumentNullException(nameof(documentVersionCache));
            }

            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _documentResolver = documentResolver;
            _razorFormattingService = razorFormattingService;
            _documentVersionCache = documentVersionCache;
        }

        public override string Action => LanguageServerConstants.CodeActions.Default;

        public async override Task<CodeAction> ResolveAsync(
            CSharpCodeActionParams csharpParams,
            CodeAction codeAction,
            CancellationToken cancellationToken)
        {
            if (csharpParams is null)
            {
                throw new ArgumentNullException(nameof(csharpParams));
            }

            if (codeAction is null)
            {
                throw new ArgumentNullException(nameof(codeAction));
            }

            var resolvedCodeAction = await ResolveCodeActionWithServerAsync(codeAction, cancellationToken).ConfigureAwait(false);
            if (resolvedCodeAction.Edit?.DocumentChanges is null)
            {
                // Unable to resolve code action with server, return original code action
                return codeAction;
            }

            if (resolvedCodeAction.Edit.DocumentChanges.Count() != 1)
            {
                // We don't yet support multi-document code actions, return original code action
                return codeAction;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var documentSnapshot = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                _documentResolver.TryResolveDocument(csharpParams.RazorFileUri.GetAbsoluteOrUNCPath(), out var documentSnapshot);
                return documentSnapshot;
            }, cancellationToken).ConfigureAwait(false);

            if (documentSnapshot is null)
            {
                return codeAction;
            }

            var documentChanged = resolvedCodeAction.Edit.DocumentChanges.First();
            if (!documentChanged.IsTextDocumentEdit)
            {
                // Only Text Document Edit changes are supported currently, return original code action
                return codeAction;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var csharpTextEdits = documentChanged.TextDocumentEdit.Edits.ToArray();

            // Remaps the text edits from the generated C# to the razor file,
            // as well as applying appropriate formatting.
            var formattedEdits = await _razorFormattingService.ApplyFormattedEditsAsync(
                csharpParams.RazorFileUri,
                documentSnapshot,
                RazorLanguageKind.CSharp,
                csharpTextEdits,
                s_defaultFormattingOptions,
                cancellationToken,
                bypassValidationPasses: true);

            cancellationToken.ThrowIfCancellationRequested();

            var documentVersion = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                _documentVersionCache.TryGetDocumentVersion(documentSnapshot, out var version);
                return version;
            }, cancellationToken).ConfigureAwait(false);

            var codeDocumentIdentifier = new OptionalVersionedTextDocumentIdentifier()
            {
                Uri = csharpParams.RazorFileUri,
                Version = documentVersion.Value
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
                                Edits = formattedEdits,
                            })
                    }
                }
            };

            return resolvedCodeAction;
        }
    }
}
