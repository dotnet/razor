// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal class DefaultCSharpCodeActionResolver : CSharpCodeActionResolver
    {
        // Usually when we need to format code, we utilize the formatting options provided
        // by the platform. However, we aren't provided such options in the case of code actions
        // so we use a default (and commonly used) configuration.
        private static readonly FormattingOptions DefaultFormattingOptions = new FormattingOptions()
        {
            TabSize = 4,
            InsertSpaces = true,
            TrimTrailingWhitespace = true,
            InsertFinalNewline = true,
            TrimFinalNewlines = true
        };

        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly RazorFormattingService _razorFormattingService;
        private readonly DocumentVersionCache _documentVersionCache;
        private readonly RazorDocumentMappingService _documentMappingService;

        public DefaultCSharpCodeActionResolver(
            ForegroundDispatcher foregroundDispatcher,
            DocumentResolver documentResolver,
            ClientNotifierServiceBase languageServer,
            RazorFormattingService razorFormattingService,
            DocumentVersionCache documentVersionCache,
            RazorDocumentMappingService documentMappingService)
            : base(languageServer)
        {
            if (foregroundDispatcher is null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
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

            if (documentMappingService is null)
            {
                throw new ArgumentNullException(nameof(documentMappingService));
            }

            _foregroundDispatcher = foregroundDispatcher;
            _documentResolver = documentResolver;
            _razorFormattingService = razorFormattingService;
            _documentVersionCache = documentVersionCache;
            _documentMappingService = documentMappingService;
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

            var resolvedCodeAction = await ResolveCodeActionWithServerAsync(codeAction, cancellationToken);
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

            var documentSnapshot = await Task.Factory.StartNew(() =>
            {
                _documentResolver.TryResolveDocument(csharpParams.RazorFileUri.GetAbsoluteOrUNCPath(), out var documentSnapshot);
                return documentSnapshot;
            }, cancellationToken, TaskCreationOptions.None, _foregroundDispatcher.ForegroundScheduler).ConfigureAwait(false);

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
                DefaultFormattingOptions,
                cancellationToken,
                bypassValidationPasses: true);

            cancellationToken.ThrowIfCancellationRequested();

            if (formattedEdits?.Length == 0 ||
                await DoesFormattingChangeNonWhitespaceContentAsync(documentSnapshot, csharpTextEdits, formattedEdits))
            {
                return codeAction;
            }

            var documentVersion = await Task.Factory.StartNew(() =>
            {
                _documentVersionCache.TryGetDocumentVersion(documentSnapshot, out var version);
                return version;
            }, cancellationToken, TaskCreationOptions.None, _foregroundDispatcher.ForegroundScheduler).ConfigureAwait(false);

            var codeDocumentIdentifier = new VersionedTextDocumentIdentifier()
            {
                Uri = csharpParams.RazorFileUri,
                Version = documentVersion.Value
            };

            resolvedCodeAction.Edit = new WorkspaceEdit()
            {
                DocumentChanges = new[] {
                    new WorkspaceEditDocumentChange(
                        new TextDocumentEdit()
                        {
                            TextDocument = codeDocumentIdentifier,
                            Edits = formattedEdits,
                        }
                    )
                }
            };

            return resolvedCodeAction;
        }

        private async Task<bool> DoesFormattingChangeNonWhitespaceContentAsync(DocumentSnapshot documentSnapshot, TextEdit[] csharpTextEdits, TextEdit[] formattedEdits)
        {
            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync();

            var originalSourceText = await documentSnapshot.GetTextAsync();

            var remappedCSharpTextEdits = RemapTextEdits(codeDocument, csharpTextEdits);
            var unformattedChanges = remappedCSharpTextEdits.Select(e => e.AsTextChange(originalSourceText));
            var expectedUnformattedSourceText = originalSourceText.WithChanges(unformattedChanges);

            var formattedChanges = formattedEdits.Select(e => e.AsTextChange(originalSourceText));
            var actualFormattedSourceText = originalSourceText.WithChanges(formattedChanges);

            return !expectedUnformattedSourceText.NonWhitespaceContentEquals(actualFormattedSourceText);

            //var originalEditLength = GetNonWhitespaceLengthOfEdits(csharpTextEdits);
            //var formattedEditLength = GetNonWhitespaceLengthOfEdits(formattedEdits);

            //return originalEditLength != formattedEditLength;
        }

        //private static int GetNonWhitespaceLengthOfEdits(TextEdit[] edits)
        //{
        //    var length = 0;

        //    foreach (var edit in edits)
        //    {
        //        foreach (var c in edit.NewText)
        //        {
        //            if (!char.IsWhiteSpace(c))
        //            {
        //                length++;
        //            }
        //        }
        //    }

        //    return length;
        //}


        protected TextEdit[] RemapTextEdits(RazorCodeDocument codeDocument, TextEdit[] projectedTextEdits)
        {
            if (projectedTextEdits is null)
            {
                throw new ArgumentNullException(nameof(projectedTextEdits));
            }

            var edits = new List<TextEdit>();
            for (var i = 0; i < projectedTextEdits.Length; i++)
            {
                var projectedRange = projectedTextEdits[i].Range;
                if (codeDocument.IsUnsupported() ||
                    !_documentMappingService.TryMapFromProjectedDocumentRange(codeDocument, projectedRange, out var originalRange))
                {
                    // Can't map range. Discard this edit.
                    continue;
                }

                var edit = new TextEdit()
                {
                    Range = originalRange,
                    NewText = projectedTextEdits[i].NewText
                };

                edits.Add(edit);
            }

            return edits.ToArray();
        }
    }
}
