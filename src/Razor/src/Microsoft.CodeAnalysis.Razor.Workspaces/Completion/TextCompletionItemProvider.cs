// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.CodeAnalysis.Razor.Completion
{
    [Shared]
    [Export(typeof(RazorCompletionItemProvider))]
    internal class TextCompletionItemProvider : RazorCompletionItemProvider
    {
        private static readonly string TextTag = "text";
        private static readonly HashSet<string> ElementCommitCharacters = new HashSet<string>{" ", ">"};
        private readonly HtmlFactsService _htmlFactsService;

        public TextCompletionItemProvider(HtmlFactsService htmlFactsService)
        {
            if (htmlFactsService is null)
            {
                throw new ArgumentNullException(nameof(htmlFactsService));
            }

            _htmlFactsService = htmlFactsService;
        }

        public override IReadOnlyList<RazorCompletionItem> GetCompletionItems(RazorSyntaxTree syntaxTree, TagHelperDocumentContext tagHelperDocumentContext, SourceSpan location)
        {
            var completions = new List<RazorCompletionItem>();
            if (syntaxTree == null)
            {
                return completions;
            }

            var change = new SourceChange(location, string.Empty);
            var owner = syntaxTree.Root.LocateOwner(change);

            if (owner == null)
            {
                Debug.Fail("Owner should never be null.");
                return completions;
            }

            if (AtTextCompletionPoint(owner))
            {
                var parent = owner.Parent;
                if (_htmlFactsService.TryGetElementInfo(parent, out var containingTagNameToken, out var attributes) &&
                    containingTagNameToken.Span.IntersectsWith(location.AbsoluteIndex))
                {
                    if (TextTag.StartsWith(containingTagNameToken.Content, StringComparison.OrdinalIgnoreCase)) {
                        var completionDisplayText = TextTag;
                        var completionItem = new RazorCompletionItem(
                            completionDisplayText,
                            completionDisplayText,
                            RazorCompletionItemKind.Text,
                            ElementCommitCharacters);
                        var completionDescription = new DirectiveCompletionDescription("Create a inline HTML element without tags.");
                        completionItem.SetDirectiveCompletionDescription(completionDescription);
                        completions.Add(completionItem);
                    }
                }
            }

            return completions;
        }

        internal static bool AtTextCompletionPoint(Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode owner)
        {
            // Only provide IntelliSense for C# code blocks, of the form:
            // @{ }, @code{ }, @functions{ }, @if(true){ }
            var implicitExpression = owner.FirstAncestorOrSelf<CSharpCodeBlockSyntax>();
            if (implicitExpression == null)
            {
                return false;
            }

            if (implicitExpression.FirstAncestorOrSelf<MarkupElementSyntax>() != null)
            {
                // Implicit expression is nested in an HTML element
                return false;
            }

            if (implicitExpression.FirstAncestorOrSelf<MarkupTagHelperElementSyntax>() != null)
            {
                // Implicit expression is nested in a TagHelper
                return false;
            }

            return true;
        }
    }
}
