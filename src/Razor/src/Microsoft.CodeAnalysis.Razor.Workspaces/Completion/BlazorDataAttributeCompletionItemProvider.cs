// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.Tooltip;
using Microsoft.VisualStudio.Editor.Razor;

namespace Microsoft.CodeAnalysis.Razor.Completion;

/// <summary>
/// Provides completions for Blazor-specific data-* attributes used for enhanced navigation and form handling.
/// </summary>
internal class BlazorDataAttributeCompletionItemProvider : IRazorCompletionItemProvider
{
    private static readonly ImmutableArray<RazorCommitCharacter> AttributeCommitCharacters = RazorCommitCharacter.CreateArray(["="]);
    private static readonly ImmutableArray<RazorCommitCharacter> AttributeSnippetCommitCharacters = RazorCommitCharacter.CreateArray(["="], insert: false);

    // Define the Blazor-specific data attributes
    private static readonly ImmutableArray<(string Name, string Description)> s_blazorDataAttributes =
    [
        ("data-enhance", "Opts in to enhanced form handling for a form element."),
        ("data-enhance-nav", "Disables enhanced navigation for a link or DOM subtree."),
        ("data-permanent", "Marks an element to be preserved when handling enhanced navigation or form requests.")
    ];

    public ImmutableArray<RazorCompletionItem> GetCompletionItems(RazorCompletionContext context)
    {
        // Only provide completions for component files
        if (!context.SyntaxTree.Options.FileKind.IsComponent())
        {
            return [];
        }

        var owner = context.Owner;
        if (owner is null)
        {
            return [];
        }

        // Adjust owner similar to TagHelperCompletionProvider
        owner = owner switch
        {
            MarkupStartTagSyntax or MarkupEndTagSyntax or MarkupTagHelperStartTagSyntax or MarkupTagHelperEndTagSyntax or MarkupTagHelperAttributeSyntax => owner,
            RazorDocumentSyntax => owner,
            _ => owner.Parent
        };

        // Check if we're in an attribute context
        if (!HtmlFacts.TryGetAttributeInfo(
                owner,
                out var containingTagNameToken,
                out var prefixLocation,
                out var selectedAttributeName,
                out var selectedAttributeNameLocation,
                out var attributes))
        {
            return [];
        }

        // Only provide completions when we're completing an attribute name
        // Similar to TagHelperCompletionProvider logic
        if (!(selectedAttributeName is null ||
            selectedAttributeNameLocation?.IntersectsWith(context.AbsoluteIndex) == true ||
            (prefixLocation?.IntersectsWith(context.AbsoluteIndex) ?? false)))
        {
            return [];
        }

        // Don't provide completions if the user is typing a directive attribute (starts with @)
        if (selectedAttributeName?.StartsWith("@", System.StringComparison.Ordinal) == true)
        {
            return [];
        }

        var containingTagName = containingTagNameToken.Content;

        using var completionItems = new PooledArrayBuilder<RazorCompletionItem>();

        foreach (var (attributeName, description) in s_blazorDataAttributes)
        {
            // Only show data-enhance for form elements
            if (attributeName == "data-enhance" &&
                !string.Equals(containingTagName, "form", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Check if the attribute already exists on the element
            var alreadyExists = false;
            foreach (var attribute in attributes)
            {
                var existingAttributeName = attribute switch
                {
                    MarkupAttributeBlockSyntax attributeBlock => attributeBlock.Name.GetContent(),
                    MarkupMinimizedAttributeBlockSyntax minimizedAttributeBlock => minimizedAttributeBlock.Name.GetContent(),
                    _ => null
                };

                if (existingAttributeName != null && string.Equals(existingAttributeName, attributeName, System.StringComparison.Ordinal))
                {
                    alreadyExists = true;
                    break;
                }
            }

            if (alreadyExists && selectedAttributeName != attributeName)
            {
                // Attribute already exists and is not the one currently being edited
                continue;
            }

            var insertText = attributeName;
            var isSnippet = false;

            // Add snippet text for attribute value if snippets are supported
            if (context.Options.SnippetsSupported)
            {
                var snippetSuffix = context.Options.AutoInsertAttributeQuotes ? "=\"$0\"" : "=$0";
                insertText = attributeName + snippetSuffix;
                isSnippet = true;
            }

            // VSCode doesn't use commit characters for attribute completions
            var commitCharacters = context.Options.UseVsCodeCompletionCommitCharacters
                ? ImmutableArray<RazorCommitCharacter>.Empty
                : (isSnippet ? AttributeSnippetCommitCharacters : AttributeCommitCharacters);

            var descriptionInfo = new BoundAttributeDescriptionInfo(
                ReturnTypeName: "bool",
                TypeName: "Blazor",
                PropertyName: attributeName,
                Documentation: description);

            var completionItem = RazorCompletionItem.CreateTagHelperAttribute(
                displayText: attributeName,
                insertText: insertText,
                sortText: null,
                descriptionInfo: new AggregateBoundAttributeDescription([descriptionInfo]),
                commitCharacters: commitCharacters,
                isSnippet: isSnippet);

            completionItems.Add(completionItem);
        }

        return completionItems.ToImmutable();
    }
}
