// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal class TypeAccessibilityAddUsingCSharpCodeActionResolver : CSharpCodeActionResolver
    {
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly DocumentVersionCache _documentVersionCache;

        private static readonly Regex AddUsingVSCodeAction = new Regex("using (.+);", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        public TypeAccessibilityAddUsingCSharpCodeActionResolver(
            ForegroundDispatcher foregroundDispatcher,
            DocumentResolver documentResolver,
            ClientNotifierServiceBase languageServer,
            DocumentVersionCache documentVersionCache)
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

            if (documentVersionCache is null)
            {
                throw new ArgumentNullException(nameof(documentVersionCache));
            }

            _foregroundDispatcher = foregroundDispatcher;
            _documentResolver = documentResolver;
            _documentVersionCache = documentVersionCache;
        }

        public override string Action => LanguageServerConstants.CodeActions.AddUsing;

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

            var documentChanged = resolvedCodeAction.Edit.DocumentChanges.First();
            if (!documentChanged.IsTextDocumentEdit)
            {
                // Only Text Document Edit changes are supported currently, return original code action
                return codeAction;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var addUsingTextEdit = documentChanged.TextDocumentEdit.Edits.FirstOrDefault();
            if (addUsingTextEdit is null)
            {
                // No text edit available
                return codeAction;
            }

            var regexMatchedTextEdit = AddUsingVSCodeAction.Match(addUsingTextEdit.NewText);
            if (!regexMatchedTextEdit.Success ||

                // Two Regex matching groups are expected
                // 1. `using namespace;`
                // 2. `namespace`
                regexMatchedTextEdit.Groups.Count != 2)
            {
                // Text edit in an unexpected format
                return codeAction;
            }

            var @namespace = regexMatchedTextEdit.Groups[1].Value;

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

            var text = await documentSnapshot.GetTextAsync().ConfigureAwait(false);
            if (text is null)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
            if (codeDocument.IsUnsupported())
            {
                return null;
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

            resolvedCodeAction.Edit = AddUsingsCodeActionResolver.CreateAddUsingWorkspaceEdit(@namespace, codeDocument, codeDocumentIdentifier);

            return resolvedCodeAction;
        }
    }
}
