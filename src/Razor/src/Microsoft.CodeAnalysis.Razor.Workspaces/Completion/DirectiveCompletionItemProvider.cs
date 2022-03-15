// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.CodeAnalysis.Razor.Completion
{
    [Shared]
    [Export(typeof(RazorCompletionItemProvider))]
    internal class DirectiveCompletionItemProvider : RazorCompletionItemProvider
    {
        internal static readonly IReadOnlyCollection<string> SingleLineDirectiveCommitCharacters = new string[] { " " };
        internal static readonly IReadOnlyCollection<string> BlockDirectiveCommitCharacters = new string[] { " ", "{" };

        private static readonly IEnumerable<DirectiveDescriptor> s_defaultDirectives = new[]
        {
            CSharpCodeParser.AddTagHelperDirectiveDescriptor,
            CSharpCodeParser.RemoveTagHelperDirectiveDescriptor,
            CSharpCodeParser.TagHelperPrefixDirectiveDescriptor,
        };

        public override IReadOnlyList<RazorCompletionItem> GetCompletionItems(RazorCompletionContext context!!, SourceSpan location)
        {
            var completions = new List<RazorCompletionItem>();
            if (ShouldProvideCompletions(context, location))
            {
                var directiveCompletions = GetDirectiveCompletionItems(context.SyntaxTree);
                completions.AddRange(directiveCompletions);
            }

            return completions;
        }

        // Internal for testing
        internal static bool ShouldProvideCompletions(RazorCompletionContext context, SourceSpan location)
        {
            if (context is null)
            {
                return false;
            }

            var change = new SourceChange(location, string.Empty);
            var owner = context.SyntaxTree.Root.LocateOwner(change);

            if (owner is null)
            {
                return false;
            }

            // Do not provide IntelliSense for explicit expressions. Explicit expressions will usually look like:
            // [@] [(] [DateTime.Now] [)]
            var implicitExpression = owner.FirstAncestorOrSelf<CSharpImplicitExpressionSyntax>();
            if (implicitExpression is null)
            {
                return false;
            }

            if (implicitExpression.FullWidth > 2 && context.Reason != CompletionReason.Invoked)
            {
                // We only want to provide directive completions if the implicit expression is empty "@|" or at the beginning of a word "@i|", this ensures
                // we're consistent with how C# typically provides completion items.
                return false;
            }

            if (owner.ChildNodes().Any(n => !n.IsToken || !IsDirectiveCompletableToken((AspNetCore.Razor.Language.Syntax.SyntaxToken)n)))
            {
                // Implicit expression contains invalid directive tokens
                return false;
            }

            if (implicitExpression.FirstAncestorOrSelf<RazorDirectiveSyntax>() != null)
            {
                // Implicit expression is nested in a directive
                return false;
            }

            if (implicitExpression.FirstAncestorOrSelf<CSharpStatementSyntax>() != null)
            {
                // Implicit expression is nested in a statement
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

        // Internal for testing
        internal static List<RazorCompletionItem> GetDirectiveCompletionItems(RazorSyntaxTree syntaxTree)
        {
            var defaultDirectives = FileKinds.IsComponent(syntaxTree.Options.FileKind) ? Array.Empty<DirectiveDescriptor>() : s_defaultDirectives;
            var directives = syntaxTree.Options.Directives.Concat(defaultDirectives);
            var completionItems = new List<RazorCompletionItem>();
            foreach (var directive in directives)
            {
                var completionDisplayText = directive.DisplayName ?? directive.Directive;
                var commitCharacters = GetDirectiveCommitCharacters(directive.Kind);
                var completionItem = new RazorCompletionItem(
                    completionDisplayText,
                    directive.Directive,
                    RazorCompletionItemKind.Directive,
                    commitCharacters: commitCharacters);
                var completionDescription = new DirectiveCompletionDescription(directive.Description);
                completionItem.SetDirectiveCompletionDescription(completionDescription);
                completionItems.Add(completionItem);
            }

            return completionItems;
        }

        private static IReadOnlyCollection<string> GetDirectiveCommitCharacters(DirectiveKind directiveKind)
        {
            return directiveKind switch
            {
                DirectiveKind.CodeBlock or DirectiveKind.RazorBlock => BlockDirectiveCommitCharacters,
                _ => SingleLineDirectiveCommitCharacters,
            };
        }

        // Internal for testing
        internal static bool IsDirectiveCompletableToken(AspNetCore.Razor.Language.Syntax.SyntaxToken token)
        {
            return token.Kind == SyntaxKind.Identifier ||
                // Marker symbol
                token.Kind == SyntaxKind.Marker ||
                token.Kind == SyntaxKind.Keyword;
        }
    }
}
