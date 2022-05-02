﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.CodeAnalysis.Razor.Completion;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal class DirectiveAttributeTransitionCompletionItemProvider : DirectiveAttributeCompletionItemProviderBase
    {
        private static RazorCompletionItem s_transitionCompletionItem;

        public static RazorCompletionItem TransitionCompletionItem
        {
            get
            {
                if (s_transitionCompletionItem is null)
                {
                    s_transitionCompletionItem = new RazorCompletionItem(
                        displayText: "@...",
                        insertText: "@",
                        kind: RazorCompletionItemKind.Directive,

                        // We specify these three commit characters to work around a Visual Studio interaction where
                        // completion items that get "soft selected" will cause completion to re-trigger if a user
                        // types one of the soft-selected completion item's commit characters.
                        // In practice this happens in the `<button |` scenario where the "space" results in completions
                        // where this directive attribute transition character ("@...") gets provided and then typing
                        // `@` should re-trigger OR typing `/` should re-trigger.
                        commitCharacters: RazorCommitCharacter.FromArray(new[] { "@", "/", ">" }));
                    s_transitionCompletionItem.SetDirectiveCompletionDescription(new DirectiveCompletionDescription(RazorLS.Resources.Blazor_directive_attributes));
                }

                return s_transitionCompletionItem;
            }
        }

        private static readonly IReadOnlyList<RazorCompletionItem> s_completions = new[] { TransitionCompletionItem };

        public override IReadOnlyList<RazorCompletionItem> GetCompletionItems(RazorCompletionContext context, SourceSpan location)
        {
            if (!FileKinds.IsComponent(context.SyntaxTree.Options.FileKind))
            {
                // Directive attributes are only supported in components
                return Array.Empty<RazorCompletionItem>();
            }

            var change = new SourceChange(location, string.Empty);
            var owner = context.SyntaxTree.Root.LocateOwner(change);

            if (owner is null)
            {
                return Array.Empty<RazorCompletionItem>();
            }

            var attribute = owner.Parent;
            if (attribute is MarkupMiscAttributeContentSyntax && attribute.ContainsOnlyWhitespace())
            {
                // This represents a tag when there's no attribute content <InputText | />.
                return s_completions;
            }

            if (!TryGetAttributeInfo(owner, out var prefixLocation, out var attributeName, out var attributeNameLocation, out _, out _))
            {
                return Array.Empty<RazorCompletionItem>();
            }

            if (attributeNameLocation.IntersectsWith(location.AbsoluteIndex) && attributeName.StartsWith("@", StringComparison.Ordinal))
            {
                // The transition is already provided for the attribute name
                return Array.Empty<RazorCompletionItem>();
            }

            if (!IsValidCompletionPoint(location, prefixLocation, attributeNameLocation))
            {
                // Not operating in the attribute name area
                return Array.Empty<RazorCompletionItem>();
            }

            // This represents a tag when there's no attribute content <InputText | />.
            return s_completions;
        }

        // Internal for testing
        internal static bool IsValidCompletionPoint(SourceSpan location, TextSpan? prefixLocation, TextSpan attributeNameLocation)
        {
            if (location.AbsoluteIndex == (prefixLocation?.Start ?? -1))
            {
                // <input| class="test" />
                // Starts of prefix locations belong to the previous SyntaxNode. It could be the end of an attribute value, the tag name, C# etc.
                return false;
            }

            if (attributeNameLocation.Start == location.AbsoluteIndex)
            {
                // <input |class="test" />
                return false;
            }

            if (prefixLocation?.IntersectsWith(location.AbsoluteIndex) ?? false)
            {
                // <input   |  class="test" />
                return true;
            }

            if (attributeNameLocation.IntersectsWith(location.AbsoluteIndex))
            {
                // <input cla|ss="test" />
                return false;
            }

            return false;
        }
    }
}
