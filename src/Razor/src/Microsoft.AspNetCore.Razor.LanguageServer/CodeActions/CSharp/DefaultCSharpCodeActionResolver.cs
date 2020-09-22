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
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal class DefaultCSharpCodeActionResolver : CSharpCodeActionResolver
    {
        private readonly RazorDocumentMappingService _documentMappingService;
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly DocumentResolver _documentResolver;

        public DefaultCSharpCodeActionResolver(
            RazorDocumentMappingService documentMappingService,
            ForegroundDispatcher foregroundDispatcher,
            DocumentResolver documentResolver,
            IClientLanguageServer languageServer)
            : base(languageServer)
        {
            if (documentMappingService is null)
            {
                throw new ArgumentNullException(nameof(documentMappingService));
            }

            if (foregroundDispatcher is null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            if (documentResolver is null)
            {
                throw new ArgumentNullException(nameof(documentResolver));
            }

            _documentMappingService = documentMappingService;
            _foregroundDispatcher = foregroundDispatcher;
            _documentResolver = documentResolver;
        }

        public override string Action => LanguageServerConstants.CodeActions.Default;

        public async override Task<RazorCodeAction> ResolveAsync(
            CSharpCodeActionParams csharpParams,
            RazorCodeAction codeAction,
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

            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
            var generatedCode = codeDocument.GetCSharpDocument().GeneratedCode;

            cancellationToken.ThrowIfCancellationRequested();

            var oldText = SourceText.From(generatedCode);
            var newText = SourceText.From(resolvedCodeAction.Edit.DocumentChanges.First().TextDocumentEdit.Edits.First().NewText);

            var changes = SourceTextDiffer.GetMinimalTextChanges(oldText, newText);

            var edits = new List<TextEdit>();

            foreach (var change in changes)
            {
                var csharpTextEdit = change.AsTextEdit(oldText);

                if (_documentMappingService.TryMapFromProjectedDocumentRange(codeDocument, csharpTextEdit.Range, out var razorTextEditRange))
                {
                    csharpTextEdit.Range = razorTextEditRange;
                    edits.Add(csharpTextEdit);
                }
            }

            var codeDocumentIdentifier = new VersionedTextDocumentIdentifier() { Uri = csharpParams.RazorFileUri };
            resolvedCodeAction.Edit = new WorkspaceEdit()
            {
                DocumentChanges = new[] {
                    new WorkspaceEditDocumentChange(
                        new TextDocumentEdit()
                        {
                            TextDocument = codeDocumentIdentifier,
                            Edits = edits,
                        }
                    )
                }
            };

            return resolvedCodeAction;
        }
    }
}
