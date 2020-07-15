// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    class RazorComponentRenameEndpoint : IRenameHandler
    {
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly ILogger _logger;

        private RenameCapability _capability;

        public RazorComponentRenameEndpoint(
            ForegroundDispatcher foregroundDispatcher,
            DocumentResolver documentResolver,
            ILoggerFactory loggerFactory)
        {
            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _foregroundDispatcher = foregroundDispatcher ?? throw new ArgumentNullException(nameof(foregroundDispatcher));
            _documentResolver = documentResolver ?? throw new ArgumentNullException(nameof(documentResolver));
            _logger = loggerFactory.CreateLogger<RazorComponentRenameEndpoint>();
        }

        public RenameRegistrationOptions GetRegistrationOptions()
        {
            return new RenameRegistrationOptions
            {
                PrepareProvider = false,
                DocumentSelector = RazorDefaults.Selector,
            };
        }

        public async Task<WorkspaceEdit> Handle(RenameParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var documentSnapshot = await Task.Factory.StartNew(() =>
            {
                _documentResolver.TryResolveDocument(request.TextDocument.Uri.GetAbsoluteOrUNCPath(), out var documentSnapshot);
                return documentSnapshot;
            }, cancellationToken, TaskCreationOptions.None, _foregroundDispatcher.ForegroundScheduler).ConfigureAwait(false);

            if (documentSnapshot is null)
            {
                return null;
            }

            // RazorComponentSearchEngine.Execute()

            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            RazorComponentSearchEngine.Execute(documentSnapshot);

            if (!FileKinds.IsComponent(codeDocument.GetFileKind()))
            {
                return null;
            }

            var originTagHelperBinding = await GetOriginTagHelperBindingAsync(documentSnapshot, codeDocument, request.Position).ConfigureAwait(false);
            var documentChanges = new List<WorkspaceEditDocumentChange>();

            AddEditsForCodeDocument(documentChanges, originTagHelperBinding, request.TextDocument.Uri, codeDocument, request.NewName);

            return new WorkspaceEdit
            {
                DocumentChanges = documentChanges,
            };
        }

        public void AddEditsForCodeDocument(List<WorkspaceEditDocumentChange> documentChanges, TagHelperBinding originTagHelperBinding, Uri uri, RazorCodeDocument codeDocument, string newName)
        {
            var documentIdentifier = new VersionedTextDocumentIdentifier { Uri = uri };
            foreach (var node in codeDocument.GetSyntaxTree().Root.DescendantNodes().Where(n => n.Kind == SyntaxKind.MarkupTagHelperElement))
            {
                if (node is MarkupTagHelperElementSyntax tagHelperElement && BindingsMatch(originTagHelperBinding, tagHelperElement.TagHelperInfo.BindingResult))
                {
                    documentChanges.Add(new WorkspaceEditDocumentChange(new TextDocumentEdit
                    {
                        TextDocument = documentIdentifier,
                        Edits = CreateEditsForMarkupTagHelperElement(tagHelperElement, codeDocument, newName)
                    }));
                }
            }
        }

        public List<TextEdit> CreateEditsForMarkupTagHelperElement(MarkupTagHelperElementSyntax element, RazorCodeDocument codeDocument, string newName)
        {
            var edits = new List<TextEdit>
            {
                new TextEdit()
                {
                    Range = element.StartTag.Name.GetRange(codeDocument.Source),
                    NewText = newName,
                },
            };
            if (element.EndTag != null)
            {
                edits.Add(new TextEdit()
                {
                    Range = element.EndTag.Name.GetRange(codeDocument.Source),
                    NewText = newName,
                });
            }
            return edits;
        }

        private static bool BindingsMatch(TagHelperBinding left, TagHelperBinding right)
        {
            foreach (var leftDescriptor in left.Descriptors)
            {
                foreach (var rightDescriptor in right.Descriptors)
                {
                    if (leftDescriptor.Equals(rightDescriptor))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private async Task<TagHelperBinding> GetOriginTagHelperBindingAsync(DocumentSnapshot documentSnapshot, RazorCodeDocument codeDocument, Position position)
        {
            var sourceText = await documentSnapshot.GetTextAsync().ConfigureAwait(false);
            var linePosition = new LinePosition((int)position.Line, (int)position.Character);
            var hostDocumentIndex = sourceText.Lines.GetPosition(linePosition);
            var location = new SourceLocation(hostDocumentIndex, (int)position.Line, (int)position.Character);

            var change = new SourceChange(location.AbsoluteIndex, length: 0, newText: string.Empty);
            var syntaxTree = codeDocument.GetSyntaxTree();
            if (syntaxTree?.Root is null)
            {
                return null;
            }

            var owner = syntaxTree.Root.LocateOwner(change);
            var node = owner.Ancestors().FirstOrDefault(n => n.Kind == SyntaxKind.MarkupTagHelperElement);
            if (node == null || !(node is MarkupTagHelperElementSyntax tagHelperElement))
            {
                return null;
            }

            return tagHelperElement.TagHelperInfo.BindingResult;
        }

        public void SetCapability(RenameCapability capability)
        {
            _capability = capability;
        }
    }
}
 