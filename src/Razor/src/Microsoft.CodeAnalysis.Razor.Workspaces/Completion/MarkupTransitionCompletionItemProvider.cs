// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using RazorSyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.CodeAnalysis.Razor.Completion
{
    internal class MarkupTransitionCompletionItemProvider : RazorCompletionItemProvider
    {
        private static readonly IReadOnlyCollection<string> ElementCommitCharacters = new HashSet<string>{">"};
        private readonly HtmlFactsService _htmlFactsService;

        public MarkupTransitionCompletionItemProvider(HtmlFactsService htmlFactsService)
        {
            if (htmlFactsService is null)
            {
                throw new ArgumentNullException(nameof(htmlFactsService));
            }

            _htmlFactsService = htmlFactsService;
        }

        public override IReadOnlyList<RazorCompletionItem> GetCompletionItems(RazorSyntaxTree syntaxTree, TagHelperDocumentContext tagHelperDocumentContext, SourceSpan location)
        {
            if (syntaxTree is null)
            {
                throw new ArgumentNullException(nameof(syntaxTree));
            }

            var change = new SourceChange(location, string.Empty);
            var owner = syntaxTree.Root.LocateOwner(change);

            if (owner == null)
            {
                Debug.Fail("Owner should never be null.");
                return Array.Empty<RazorCompletionItem>();
            }

            if (!AtMarkupTransitionCompletionPoint(owner))
            {
                return Array.Empty<RazorCompletionItem>();
            }

            var parent = owner.Parent;
            if (!_htmlFactsService.TryGetElementInfo(parent, out var containingTagNameToken, out var attributes) ||
                !containingTagNameToken.Span.IntersectsWith(location.AbsoluteIndex))
            {
                return Array.Empty<RazorCompletionItem>();
            }

            var completions = new List<RazorCompletionItem>();
            var completionDisplayText = SyntaxConstants.TextTagName;
            var completionItem = new RazorCompletionItem(
                completionDisplayText,
                completionDisplayText,
                RazorCompletionItemKind.MarkupTransition,
                ElementCommitCharacters);
            var completionDescription = new MarkupTransitionCompletionDescription(CSharpCodeParser.AddMarkupTransitionDescriptor);
            completionItem.SetMarkupTransitionCompletionDescription(completionDescription);
            completions.Add(completionItem);

            return completions;
        }

        // Internal for testing
        internal static bool AtMarkupTransitionCompletionPoint(RazorSyntaxNode owner)
        {
            // Only provide IntelliSense for C# code blocks, of the form:
            // @{ }, @code{ }, @functions{ }, @if(true){ }
            var closestSignificantAncestor = owner.Ancestors().FirstOrDefault(node =>
            {
                return (node is MarkupElementSyntax markupNode && markupNode.ChildNodes().Count != 1) ||
                        node is MarkupMinimizedAttributeBlockSyntax || // Accounts for improper markup tags ex. `< te`
                        node is CSharpCodeBlockSyntax;
            });
            return closestSignificantAncestor is CSharpCodeBlockSyntax;
        }
    }
}
