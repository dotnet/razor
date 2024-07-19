// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.LanguageServer.Protocol;

using RazorSyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.CodeAnalysis.Razor.AutoInsert;

internal sealed class AutoClosingTagOnAutoInsertProvider : IOnAutoInsertProvider
{
    // From http://dev.w3.org/html5/spec/Overview.html#elements-0
    private static readonly ImmutableHashSet<string> s_voidElements = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase,
        "area",
        "base",
        "br",
        "col",
        "command",
        "embed",
        "hr",
        "img",
        "input",
        "keygen",
        "link",
        "meta",
        "menuitem",
        "param",
        "source",
        "track",
        "wbr"
    );
    private static readonly ImmutableHashSet<string> s_voidElementsCaseSensitive = s_voidElements.WithComparer(StringComparer.Ordinal);

    private readonly ILogger _logger;

    public AutoClosingTagOnAutoInsertProvider(ILoggerFactory loggerFactory)
    {
        if (loggerFactory is null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        _logger = loggerFactory.GetOrCreateLogger<IOnAutoInsertProvider>();
    }

    public string TriggerCharacter => ">";

    public async ValueTask<InsertTextEdit?> TryResolveInsertionAsync(Position position, IDocumentSnapshot documentSnapshot, bool autoClosingTagsOption)
    {
        if (!(autoClosingTagsOption
            && documentSnapshot.TryGetText(out var sourceText)
            && position.TryGetAbsoluteIndex(sourceText, _logger, out var afterCloseAngleIndex)
            && await TryResolveAutoClosingBehaviorAsync(documentSnapshot, afterCloseAngleIndex)
                     .ConfigureAwait(false) is { } tagNameWithClosingBehavior))
        {
            return default;
        }

        if (tagNameWithClosingBehavior.AutoClosingBehavior == AutoClosingBehavior.EndTag)
        {
            var formatForEndTag = InsertTextFormat.Snippet;
            var editForEndTag = new TextEdit()
            {
                NewText = $"$0</{tagNameWithClosingBehavior.TagName}>",
                Range = new Range { Start = position, End = position },
            };

            return new InsertTextEdit(editForEndTag, formatForEndTag);
        }

        Debug.Assert(tagNameWithClosingBehavior.AutoClosingBehavior == AutoClosingBehavior.SelfClosing);

        var format = InsertTextFormat.Plaintext;

        // Need to replace the `>` with ' />$0' or '/>$0' depending on if there's prefixed whitespace.
        var insertionText = char.IsWhiteSpace(sourceText[afterCloseAngleIndex - 2]) ? "/" : " /";
        var insertionPosition = new Position(position.Line, position.Character - 1);
        var edit = new TextEdit()
        {
            NewText = insertionText,
            Range = new Range
            {
                Start = insertionPosition,
                End = insertionPosition
            }
        };

        return new InsertTextEdit(edit, format);
    }

    private static async ValueTask<TagNameWithClosingBehavior?> TryResolveAutoClosingBehaviorAsync(IDocumentSnapshot documentSnapshot, int afterCloseAngleIndex)
    {
        var codeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
        var syntaxTree = codeDocument.GetSyntaxTree();
        var closeAngle = syntaxTree.Root.FindToken(afterCloseAngleIndex - 1);

        if (closeAngle.Parent is MarkupStartTagSyntax
            {
                ForwardSlash: null,
                Parent: MarkupElementSyntax htmlElement
            } startTag)
        {
            var unescapedTagName = startTag.Name.Content;
            var autoClosingBehavior = InferAutoClosingBehavior(unescapedTagName, caseSensitive: false);

            if (autoClosingBehavior == AutoClosingBehavior.EndTag && !CouldAutoCloseParentOrSelf(unescapedTagName, htmlElement))
            {
                // Auto-closing behavior is end-tag; however, we already have and end-tag therefore we don't need to do anything!
                return default;
            }

            // Finally capture the entire tag name with the potential escape operator.
            var name = startTag.GetTagNameWithOptionalBang();
            return new TagNameWithClosingBehavior(name, autoClosingBehavior);
        }

        if (closeAngle.Parent is MarkupTagHelperStartTagSyntax
            {
                ForwardSlash: null,
                Parent: MarkupTagHelperElementSyntax { TagHelperInfo.BindingResult: var binding } tagHelperElement
            } startTagHelper)
        {
            var name = startTagHelper.Name.Content;

            if (!TryGetTagHelperAutoClosingBehavior(binding, out var autoClosingBehavior))
            {
                autoClosingBehavior = InferAutoClosingBehavior(name, caseSensitive: true);
            }

            if (autoClosingBehavior == AutoClosingBehavior.EndTag && !CouldAutoCloseParentOrSelf(name, tagHelperElement))
            {
                // Auto-closing behavior is end-tag; however, we already have and end-tag therefore we don't need to do anything!
                return default;
            }

            return new TagNameWithClosingBehavior(name, autoClosingBehavior);
        }

        return default;
    }

    private static AutoClosingBehavior InferAutoClosingBehavior(string name, bool caseSensitive)
    {
        var voidElements = caseSensitive ? s_voidElementsCaseSensitive : s_voidElements;

        if (voidElements.Contains(name))
        {
            return AutoClosingBehavior.SelfClosing;
        }

        return AutoClosingBehavior.EndTag;
    }

    private static bool TryGetTagHelperAutoClosingBehavior(TagHelperBinding bindingResult, out AutoClosingBehavior autoClosingBehavior)
    {
        var resolvedTagStructure = TagStructure.Unspecified;

        foreach (var descriptor in bindingResult.Descriptors)
        {
            var tagMatchingRules = bindingResult.Mappings[descriptor];
            foreach (var tagMatchingRule in tagMatchingRules)
            {
                if (tagMatchingRule.TagStructure == TagStructure.Unspecified)
                {
                    // The current tag matching rule isn't specified so it should never be used as the resolved tag structure since it
                    // says it doesn't have an opinion.
                }
                else if (tagMatchingRule.TagStructure == TagStructure.NormalOrSelfClosing)
                {
                    // We have a rule that indicates it can be normal or self-closing, that always wins because
                    // it's all encompassing. Meaning, even if all previous rules indicate "no children" and at least
                    // one says it supports children we render the tag as having the potential to have children.
                    autoClosingBehavior = AutoClosingBehavior.EndTag;
                    return true;
                }
                else
                {
                    resolvedTagStructure = tagMatchingRule.TagStructure;
                }
            }
        }

        Debug.Assert(resolvedTagStructure != TagStructure.NormalOrSelfClosing, "Normal tag structure should already have been preferred");

        if (resolvedTagStructure == TagStructure.WithoutEndTag)
        {
            autoClosingBehavior = AutoClosingBehavior.SelfClosing;
            return true;
        }

        autoClosingBehavior = default;
        return false;
    }

    private static bool CouldAutoCloseParentOrSelf(string currentTagName, RazorSyntaxNode node)
    {
        do
        {
            string? potentialStartTagName = null;
            RazorSyntaxNode? endTag = null;
            if (node is MarkupTagHelperElementSyntax parentTagHelper)
            {
                potentialStartTagName = parentTagHelper.StartTag?.Name.Content ?? parentTagHelper.EndTag?.Name.Content;
                endTag = parentTagHelper.EndTag;
            }
            else if (node is MarkupElementSyntax parentElement)
            {
                potentialStartTagName = parentElement.StartTag?.Name.Content ?? parentElement.EndTag?.Name.Content;
                endTag = parentElement.EndTag;
            }

            var isNonTagStructure = potentialStartTagName is null;
            if (isNonTagStructure)
            {
                // We don't want to look outside of our immediate parent for potential parents that we could auto-close because
                // auto-closing one of those parents wouldn't actually auto-close them. For instance:
                //
                // <div>
                //     @if (true)
                //     {
                //         <div>|</div>
                //     }
                //
                // If we re-type the `>` in the inner-div we don't want to add another </div> because it would be out of scope
                // for the parent <div>
                return false;
            }

            if (string.Equals(potentialStartTagName, currentTagName, StringComparison.Ordinal))
            {
                // Tag names equal, if the parent is missing an end-tag it could apply to that
                // i.e. <div><div>|</div>
                if (endTag is null)
                {
                    return true;
                }

                // Has an end-tag; however, it could be another level of parent which is OK lets keep going up
            }
            else
            {
                // Different tag name, can't apply
                return false;
            }

            node = node.Parent;
        } while (node is not null);

        return false;
    }

    private enum AutoClosingBehavior
    {
        EndTag,
        SelfClosing,
    }

    private record struct TagNameWithClosingBehavior(string TagName, AutoClosingBehavior AutoClosingBehavior)
    {
    }
}
