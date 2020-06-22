// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    class RefactorComponentCreateCodeActionResolver : RazorCodeActionResolver
    {
        public override string Action => Constants.RefactorComponentCreate;

        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly DocumentResolver _documentResolver;

        public RefactorComponentCreateCodeActionResolver(
            ForegroundDispatcher foregroundDispatcher,
            DocumentResolver documentResolver)
        {
            if (foregroundDispatcher is null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            if (documentResolver is null)
            {
                throw new ArgumentNullException(nameof(documentResolver));
            }

            _foregroundDispatcher = foregroundDispatcher;
            _documentResolver = documentResolver;
        }

        public override string Action => LanguageServerConstants.CodeActions.CreateComponentFromTag;

        public override async Task<WorkspaceEdit> ResolveAsync(JObject data, CancellationToken cancellationToken)
        {
            var actionParams = data.ToObject<RefactorComponentCreateParams>();
            var path = actionParams.Uri.GetAbsoluteOrUNCPath();

            var document = await Task.Factory.StartNew(() =>
            {
                _documentResolver.TryResolveDocument(path, out var documentSnapshot);
                return documentSnapshot;
            }, cancellationToken, TaskCreationOptions.None, _foregroundDispatcher.ForegroundScheduler).ConfigureAwait(false);
            if (document is null)
            {
                return null;
            }

            var text = await document.GetTextAsync().ConfigureAwait(false);
            if (text is null)
            {
                return null;
            }

            var codeDocument = await document.GetGeneratedOutputAsync().ConfigureAwait(false);
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            if (!FileKinds.IsComponent(codeDocument.GetFileKind()))
            {
                return null;
            }

            var newComponentUri = new UriBuilder()
            {
                Scheme = Uri.UriSchemeFile,
                Path = actionParams.Where,
                Host = string.Empty,
            }.Uri;

            var documentChanges = new List<WorkspaceEditDocumentChange>
            {
                new WorkspaceEditDocumentChange(new CreateFile() { Uri = newComponentUri.ToString() })
            };

            var syntaxTree = codeDocument.GetSyntaxTree();
            if (syntaxTree?.Root != null)
            {
                var namespaceDirective = syntaxTree.Root.DescendantNodes()
                    .Where(n => n.Kind == SyntaxKind.RazorDirective)
                    .Cast<RazorDirectiveSyntax>()
                    .Where(n => n.DirectiveDescriptor == NamespaceDirective.Directive)
                    .FirstOrDefault();
                if (namespaceDirective != null)
                {
                    var documentIdentifier = new VersionedTextDocumentIdentifier { Uri = newComponentUri };
                    documentChanges.Add(new WorkspaceEditDocumentChange(new TextDocumentEdit
                    {
                        TextDocument = documentIdentifier,
                        Edits = new[]
                        {
                            new TextEdit()
                            {
                                NewText = namespaceDirective.GetContent(),
                                Range = new Range(new Position(0, 0), new Position(0, 0)),
                            }
                        }
                    }));
                }
            }

            return new WorkspaceEdit()
            {
                DocumentChanges = documentChanges
            };
        }
    }
}
