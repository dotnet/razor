// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.Extensions.Options;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer.AutoInsert
{
    internal class AutoClosingTagOnAutoInsertProvider : RazorOnAutoInsertProvider
    {
        // From http://dev.w3.org/html5/spec/Overview.html#elements-0
        public static readonly HashSet<string> s_VoidElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
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
        };

        private readonly IOptionsMonitor<RazorLSPOptions> _optionsMonitor;

        public AutoClosingTagOnAutoInsertProvider(IOptionsMonitor<RazorLSPOptions> optionsMonitor)
        {
            if (optionsMonitor is null)
            {
                throw new ArgumentNullException(nameof(optionsMonitor));
            }

            _optionsMonitor = optionsMonitor;
        }

        public override string TriggerCharacter => ">";

        public override bool TryResolveInsertion(Position position, FormattingContext context, out TextEdit edit, out InsertTextFormat format)
        {
            if (position is null)
            {
                throw new ArgumentNullException(nameof(position));
            }

            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!_optionsMonitor.CurrentValue.AutoClosingTags)
            {
                format = default;
                edit = default;
                return false;
            }

            if (!TryGetTagInformation(context, position, out var tagName, out var autoClosingBehavior))
            {
                format = default;
                edit = default;
                return false;
            }

            format = InsertTextFormat.Snippet;
            if (autoClosingBehavior == AutoClosingBehavior.EndTag)
            {
                edit = new TextEdit()
                {
                    NewText = $"$0</{tagName}>",
                    Range = new Range(position, position)
                };
            }
            else
            {
                Debug.Assert(autoClosingBehavior == AutoClosingBehavior.SelfClosing);

                // Need to replace the `>` with ' />$0'
                var replacementRange = new Range(
                    start: new Position(position.Line, position.Character - 1),
                    end: position);
                edit = new TextEdit()
                {
                    NewText = " />$0",
                    Range = replacementRange
                };

            }

            return true;
        }

        private static bool TryGetTagInformation(FormattingContext context, Position position, out string name, out AutoClosingBehavior autoClosingBehavior)
        {
            var syntaxTree = context.CodeDocument.GetSyntaxTree();

            var absoluteIndex = position.GetAbsoluteIndex(context.SourceText) - 1;
            var change = new SourceChange(absoluteIndex, 0, string.Empty);
            var owner = syntaxTree.Root.LocateOwner(change);
            if (owner?.Parent == null)
            {
                name = null;
                autoClosingBehavior = default;
                return false;
            }

            if (owner.Parent is MarkupStartTagSyntax startTag &&
                startTag.Parent is MarkupElementSyntax)
            {
                name = startTag.Name.Content;
                autoClosingBehavior = InferAutoClosingBehavior(name);
                name = startTag.GetTagNameWithOptionalBang();
                return true;
            }

            if (owner.Parent is MarkupTagHelperStartTagSyntax startTagHelper &&
                startTagHelper.Parent is MarkupTagHelperElementSyntax tagHelperElement)
            {
                name = startTagHelper.Name.Content;

                if (!TryGetTagHelperAutoClosingBehavior(tagHelperElement.TagHelperInfo.BindingResult, out autoClosingBehavior))
                {
                    autoClosingBehavior = InferAutoClosingBehavior(name);
                }

                return true;
            }

            autoClosingBehavior = default;
            name = null;
            return false;
        }

        private static AutoClosingBehavior InferAutoClosingBehavior(string name)
        {
            if (s_VoidElements.Contains(name))
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
                var tagMatchingRules = bindingResult.GetBoundRules(descriptor);
                for (var i = 0; i < tagMatchingRules.Count; i++)
                {
                    var tagMatchingRule = tagMatchingRules[i];
                    if (tagMatchingRule.TagStructure == TagStructure.NormalOrSelfClosing)
                    {
                        // We have a rule that indicates it can be normal or self-closing, that always wins because.
                        // it's all encompassing. Meaning, even if all previous rules indicate "no children" and at least
                        // one says it supports children we render the tag as having the potential to have children.
                        autoClosingBehavior = AutoClosingBehavior.EndTag;
                        return true;
                    }

                    resolvedTagStructure = tagMatchingRule.TagStructure;
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

        private enum AutoClosingBehavior
        {
            EndTag,
            SelfClosing,
        }
    }
}
