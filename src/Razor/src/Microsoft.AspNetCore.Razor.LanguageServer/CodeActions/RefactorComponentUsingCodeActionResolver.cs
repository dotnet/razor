// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    class RefactorComponentUsingCodeActionResolver : RazorCodeActionResolver
    {
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly DocumentResolver _documentResolver;

        public RefactorComponentUsingCodeActionResolver(
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

        public override string Action => LanguageServerConstants.CodeActions.AddUsing;

        public override async Task<WorkspaceEdit> ResolveAsync(JObject data, CancellationToken cancellationToken)
        {
            var actionParams = data.ToObject<RefactorComponentUsingParams>();
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

            var codeDocumentIdentifier = new VersionedTextDocumentIdentifier() { Uri = actionParams.Uri, Version = 0 };
            var documentChanges = new List<WorkspaceEditDocumentChange>();

            var namespaceList = actionParams.Namespaces.ToList();
            namespaceList.Sort();
            var namespaces = new Queue<string>(namespaceList);

            var usingDirectives = FindUsingDirectives(codeDocument);
            if (usingDirectives.Count > 0)
            {
                var edits = new List<TextEdit>();
                foreach (var usingDirective in usingDirectives)
                {
                    var usingDirectiveNamespace = usingDirective.Statement.ParsedNamespace;
                    if (usingDirectiveNamespace.StartsWith("System") || usingDirectiveNamespace.Contains("="))
                    {
                        continue;
                    }
                    if (namespaces.Count == 0)
                    {
                        break;
                    }
                    if (namespaces.Peek().CompareTo(usingDirectiveNamespace) < 0)
                    {
                        var usingDirectiveLineIndex = codeDocument.Source.Lines.GetLocation(usingDirective.Node.Span.Start).LineIndex;
                        var head = new Position(usingDirectiveLineIndex, 0);
                        var edit = new TextEdit() { Range = new Range(head, head), NewText = "" };
                        do
                        {
                            edit.NewText += $"@using {namespaces.Dequeue()}\n";
                        } while (namespaces.Count > 0 && namespaces.Peek().CompareTo(usingDirectiveNamespace) < 0);
                        edits.Add(edit);
                    }
                }
                if (namespaces.Count > 0)
                {
                    var usingDirectiveLineIndex = codeDocument.Source.Lines.GetLocation(usingDirectives.Last().Node.Span.End).LineIndex;
                    var head = new Position(usingDirectiveLineIndex + 1, 0);
                    var edit = new TextEdit() { Range = new Range(head, head), NewText = "" };
                    do
                    {
                        edit.NewText += $"@using {namespaces.Dequeue()}\n";
                    } while (namespaces.Count > 0);
                    edits.Add(edit);
                }
                documentChanges.Add(new WorkspaceEditDocumentChange(new TextDocumentEdit()
                {
                    TextDocument = codeDocumentIdentifier,
                    Edits = edits,
                }));
            }
            else
            {
                var head = new Position(0, 0);
                var lastNamespaceOrPageDirective = codeDocument.GetSyntaxTree().Root
                    .DescendantNodes()
                    .Where(n => IsNamespaceOrPageDirective(n))
                    .LastOrDefault();
                if (lastNamespaceOrPageDirective != null)
                {
                    var end = codeDocument.Source.Lines.GetLocation(lastNamespaceOrPageDirective.Span.End);
                    head = new Position(end.LineIndex, 0);
                }
                var range = new Range(head, head);
                documentChanges.Add(new WorkspaceEditDocumentChange(new TextDocumentEdit
                {
                    TextDocument = codeDocumentIdentifier,
                    Edits = new[]
                        {
                            new TextEdit()
                            {
                                NewText = string.Concat(namespaces.Select(n => $"@using {n}\n")),
                                Range = range,
                            }
                        }
                }));
            }

            return new WorkspaceEdit()
            {
                DocumentChanges = documentChanges
            };
        }

        private static List<RazorUsingDirective> FindUsingDirectives(RazorCodeDocument codeDocument)
        {
            var directives = new List<RazorUsingDirective>();
            foreach (var node in codeDocument.GetSyntaxTree().Root.DescendantNodes())
            {
                if (node is RazorDirectiveSyntax directiveNode)
                {
                    foreach (var child in directiveNode.DescendantNodes())
                    {
                        var context = child.GetSpanContext();
                        if (context != null && context.ChunkGenerator is AddImportChunkGenerator usingStatement && !usingStatement.IsStatic)
                        {
                            directives.Add(new RazorUsingDirective(directiveNode, usingStatement));
                        }
                    }
                }
            }
            return directives;
        }

        private static bool IsNamespaceOrPageDirective(SyntaxNode node)
        {
            if (node is RazorDirectiveSyntax directiveNode)
            {
                return directiveNode.DirectiveDescriptor == ComponentPageDirective.Directive || directiveNode.DirectiveDescriptor == NamespaceDirective.Directive;
            }
            return false;
        }

        private struct RazorUsingDirective
        {
            readonly public RazorDirectiveSyntax Node { get; }
            readonly public AddImportChunkGenerator Statement { get; }

            public RazorUsingDirective(RazorDirectiveSyntax node, AddImportChunkGenerator statement)
            {
                Node = node;
                Statement = statement;
            }
        }
    }
}
