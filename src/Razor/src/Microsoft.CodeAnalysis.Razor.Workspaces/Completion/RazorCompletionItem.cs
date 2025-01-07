// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis.Razor.Tooltip;
using Microsoft.Extensions.Internal;

namespace Microsoft.CodeAnalysis.Razor.Completion;

internal sealed class RazorCompletionItem : IEquatable<RazorCompletionItem>
{
    public RazorCompletionItemKind Kind { get; }
    public string DisplayText { get; }
    public string InsertText { get; }

    /// <summary>
    /// A string that is used to alphabetically sort the completion item.
    /// </summary>
    public string SortText { get; }

    public object? DescriptionInfo { get; }
    public ImmutableArray<RazorCommitCharacter> CommitCharacters { get; }
    public bool IsSnippet { get; }

    /// <summary>
    /// Creates a new Razor completion item
    /// </summary>
    /// <param name="displayText">The text to display in the completion list.</param>
    /// <param name="insertText">Content to insert when completion item is committed.</param>
    /// <param name="kind">The type of completion item this is. Used for icons and resolving extra information like tooltip text.</param>
    /// <param name="descriptionInfo">An object that provides description information for this completion item.</param>
    /// <param name="sortText">A string that is used to alphabetically sort the completion item. If omitted defaults to <paramref name="displayText"/>.</param>
    /// <param name="commitCharacters">Characters that can be used to commit the completion item.</param>
    /// <param name="isSnippet">Indicates whether the completion item's <see cref="InsertText"/> is an LSP snippet or not.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="displayText"/> or <paramref name="insertText"/> are <see langword="null"/>.</exception>
    public RazorCompletionItem(
        string displayText,
        string insertText,
        RazorCompletionItemKind kind,
        object? descriptionInfo = null,
        string? sortText = null,
        ImmutableArray<RazorCommitCharacter> commitCharacters = default,
        bool isSnippet = false)
    {
        ArgHelper.ThrowIfNull(displayText);
        ArgHelper.ThrowIfNull(insertText);

        DisplayText = displayText;
        InsertText = insertText;
        Kind = kind;
        DescriptionInfo = descriptionInfo;
        CommitCharacters = commitCharacters.NullToEmpty();
        SortText = sortText ?? displayText;
        IsSnippet = isSnippet;
    }

    public static RazorCompletionItem CreateDirective(
        string displayText, string insertText, string? sortText,
        DirectiveCompletionDescription description,
        ImmutableArray<RazorCommitCharacter> commitCharacters, bool isSnippet)
        => new(displayText, insertText, RazorCompletionItemKind.Directive, description, sortText, commitCharacters, isSnippet);

    public static RazorCompletionItem CreateDirectiveAttribute(
        string displayText, string insertText,
        AggregateBoundAttributeDescription description,
        ImmutableArray<RazorCommitCharacter> commitCharacters)
        => new(displayText, insertText, RazorCompletionItemKind.DirectiveAttribute, description, sortText: null, commitCharacters, isSnippet: false);

    public static RazorCompletionItem CreateDirectiveAttributeParameter(
        string displayText, string insertText,
        AggregateBoundAttributeDescription description)
        => new(displayText, insertText, RazorCompletionItemKind.DirectiveAttributeParameter, description, sortText: null, commitCharacters: [], isSnippet: false);

    public static RazorCompletionItem CreateMarkupTransition(
        string displayText, string insertText,
        MarkupTransitionCompletionDescription description,
        ImmutableArray<RazorCommitCharacter> commitCharacters)
        => new(displayText, insertText, RazorCompletionItemKind.MarkupTransition, description, sortText: null, commitCharacters, isSnippet: false);

    public static RazorCompletionItem CreateTagHelperElement(
        string displayText, string insertText,
        AggregateBoundElementDescription description,
        ImmutableArray<RazorCommitCharacter> commitCharacters)
        => new(displayText, insertText, RazorCompletionItemKind.TagHelperElement, description, sortText: null, commitCharacters, isSnippet: false);

    public override bool Equals(object? obj)
        => Equals(obj as RazorCompletionItem);

    public bool Equals(RazorCompletionItem? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null &&
               DisplayText == other.DisplayText &&
               InsertText == other.InsertText &&
               Kind == other.Kind &&
               CommitCharacters.SequenceEqual(other.CommitCharacters);
    }

    public override int GetHashCode()
    {
        var hashCodeCombiner = HashCodeCombiner.Start();
        hashCodeCombiner.Add(DisplayText);
        hashCodeCombiner.Add(InsertText);
        hashCodeCombiner.Add(Kind);
        hashCodeCombiner.Add(CommitCharacters);

        return hashCodeCombiner.CombinedHash;
    }
}
