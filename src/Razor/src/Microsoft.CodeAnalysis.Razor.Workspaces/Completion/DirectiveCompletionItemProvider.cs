﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.CodeAnalysis.Razor.Completion;

[Shared]
[Export(typeof(IRazorCompletionItemProvider))]
internal class DirectiveCompletionItemProvider : IRazorCompletionItemProvider
{
    internal static readonly IReadOnlyList<RazorCommitCharacter> SingleLineDirectiveCommitCharacters = RazorCommitCharacter.FromArray(new[] { " " });
    internal static readonly IReadOnlyList<RazorCommitCharacter> BlockDirectiveCommitCharacters = RazorCommitCharacter.FromArray(new[] { " ", "{" });

    private static readonly IEnumerable<DirectiveDescriptor> s_defaultDirectives = new[]
    {
        CSharpCodeParser.AddTagHelperDirectiveDescriptor,
        CSharpCodeParser.RemoveTagHelperDirectiveDescriptor,
        CSharpCodeParser.TagHelperPrefixDirectiveDescriptor,
    };

    // internal for testing
    // Do not forget to update both insert and display text !important
    internal static readonly IReadOnlyDictionary<string, (string InsertText, string DisplayText)> s_singleLineDirectiveSnippets = new Dictionary<string, (string InsertText, string DisplayText)>(StringComparer.Ordinal)
    {
        ["addTagHelper"] = ("addTagHelper ${1:*}, ${2:Microsoft.AspNetCore.Mvc.TagHelpers}", "addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers"),
        ["attribute"] = ("attribute [${1:Authorize}]$0", "attribute [Authorize]"),
        ["implements"] = ("implements ${1:IDisposable}$0", "implements IDisposable"),
        ["inherits"] = ("inherits ${1:ComponentBase}$0", "inherits ComponentBase"),
        ["inject"] = ("inject ${1:IService} ${2:MyService}", "inject IService MyService"),
        ["layout"] = ("layout ${1:MainLayout}$0", "layout MainLayout"),
        ["model"] = ("model ${1:MyModelClass}$0", "model MyModelClass"),
        ["namespace"] = ("namespace ${1:MyNameSpace}$0", "namespace MyNameSpace"),
        ["page"] = ("page \"/${1:page}\"$0", "page \"/page\""),
        ["preservewhitespace"] = ("preservewhitespace ${1:true}$0", "preservewhitespace true"),
        ["removeTagHelper"] = ("removeTagHelper ${1:*}, ${2:Microsoft.AspNetCore.Mvc.TagHelpers}", "removeTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers"),
        ["tagHelperPrefix"] = ("tagHelperPrefix ${1:prefix}$0", "tagHelperPrefix prefix"),
        ["typeparam"] = ("typeparam ${1:T}$0", "typeparam T")
    };

    public ImmutableArray<RazorCompletionItem> GetCompletionItems(RazorCompletionContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        using var completions = new PooledArrayBuilder<RazorCompletionItem>();

        if (ShouldProvideCompletions(context))
        {
            var directiveCompletions = GetDirectiveCompletionItems(context.SyntaxTree);
            completions.AddRange(directiveCompletions);
        }

        return completions.DrainToImmutable();
    }

    // Internal for testing
    internal static bool ShouldProvideCompletions(RazorCompletionContext context)
    {
        if (context is null)
        {
            return false;
        }

        var owner = context.Owner;
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
    internal static ImmutableArray<RazorCompletionItem> GetDirectiveCompletionItems(RazorSyntaxTree syntaxTree)
    {
        var defaultDirectives = FileKinds.IsComponent(syntaxTree.Options.FileKind) ? Array.Empty<DirectiveDescriptor>() : s_defaultDirectives;
        var directives = syntaxTree.Options.Directives.Concat(defaultDirectives);

        using var completionItems = new PooledArrayBuilder<RazorCompletionItem>();

        foreach (var directive in directives)
        {
            var completionDisplayText = directive.DisplayName ?? directive.Directive;
            var commitCharacters = GetDirectiveCommitCharacters(directive.Kind);
            
            var completionItem = new RazorCompletionItem(
                completionDisplayText,
                directive.Directive,
                RazorCompletionItemKind.Directive,
                commitCharacters: commitCharacters,
                isSnippet: false);
            var completionDescription = new DirectiveCompletionDescription(directive.Description);
            completionItem.SetDirectiveCompletionDescription(completionDescription);
            completionItems.Add(completionItem);
            
            if (s_singleLineDirectiveSnippets.TryGetValue(directive.Directive, out var snippetTexts))
            {
                var snippetCompletionItem = new RazorCompletionItem(
                    $"{completionDisplayText} ...",
                    snippetTexts.InsertText,
                    RazorCompletionItemKind.Directive,
                    commitCharacters: commitCharacters,
                    isSnippet: true);
                
                var snippetDescription = "@" + snippetTexts.DisplayText
                                             + Environment.NewLine
                                             + SR.DirectiveSnippetDescription;

                snippetCompletionItem.SetDirectiveCompletionDescription(new(snippetDescription));
                completionItems.Add(snippetCompletionItem);
            }
        }

        return completionItems.DrainToImmutable();
    }

    private static IReadOnlyList<RazorCommitCharacter> GetDirectiveCommitCharacters(DirectiveKind directiveKind)
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
