// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.VisualStudio.Editor.Razor;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal class AttributeSnippetFormatOnTypeProvider : RazorFormatOnTypeProvider
    {
        private readonly TagHelperFactsService _tagHelperFactsService;

        public override string TriggerCharacter => "=";

        public AttributeSnippetFormatOnTypeProvider(TagHelperFactsService tagHelperFactsService)
        {
            if (tagHelperFactsService is null)
            {
                throw new ArgumentNullException(nameof(tagHelperFactsService));
            }

            _tagHelperFactsService = tagHelperFactsService;
        }

        public override bool TryFormatOnType(Position position, FormattingContext context, out TextEdit[] edits)
        {
            if (position is null)
            {
                throw new ArgumentNullException(nameof(position));
            }

            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            bool addCursorPlaceholder;
            if (context.Options.TryGetValue(LanguageServerConstants.ExpectsCursorPlaceholderKey, out var value) && value.IsBool)
            {
                addCursorPlaceholder = value.Bool;
            }
            else
            {
                // Temporary:
                // no-op if cursor placeholder isn't supported. This means the request isn't coming from VS.
                edits = null;
                return false;
            }

            if (!IsAtAttributeValueStart(context, position))
            {
                edits = null;
                return false;
            }

            var cursorPlaceholder = addCursorPlaceholder ? LanguageServerConstants.CursorPlaceholderString : string.Empty;
            var edit = new TextEdit()
            {
                NewText = $"\"{cursorPlaceholder}\"",
                Range = new Range(position, position)
            };

            edits = new[] { edit };
            return true;
        }

        private bool IsAtAttributeValueStart(FormattingContext context, Position position)
        {
            var syntaxTree = context.CodeDocument.GetSyntaxTree();

            var absoluteIndex = position.GetAbsoluteIndex(context.SourceText);
            var change = new SourceChange(absoluteIndex, 0, string.Empty);
            var owner = syntaxTree.Root.LocateOwner(change);

            if (owner?.Parent is MarkupLiteralAttributeValueSyntax)
            {
                // Accounts for
                // 1. <Counter IncrementBy=|
                // 2. <Counter IncrementBy|
                // 3. <Counter IncrementBy=|
                owner = owner.Parent;
            }

            if (owner is null ||
                !(owner.Parent is MarkupTagHelperAttributeValueSyntax attributeValue) ||
                !(owner.Parent.Parent is MarkupTagHelperAttributeSyntax attribute) ||
                !(owner.Parent.Parent.Parent is MarkupTagHelperStartTagSyntax startTag))
            {
                // Incorrect taghelper tree structure
                return false;
            }

            if (!attributeValue.Span.IsEmpty || string.IsNullOrEmpty(attribute.Name.GetContent()))
            {
                // Attribute value already started or attribute is empty
                return false;
            }

            var ancestors = owner.Ancestors();
            var (parentTag, parentIsTagHelper) = _tagHelperFactsService.GetNearestAncestorTagInfo(ancestors);
            var stringifiedAttributes = _tagHelperFactsService.StringifyAttributes(startTag.Attributes);
            var binding = _tagHelperFactsService.GetTagHelperBinding(
                context.CodeDocument.GetTagHelperContext(),
                startTag.Name.Content,
                stringifiedAttributes,
                parentTag: parentTag,
                parentIsTagHelper: parentIsTagHelper);

            if (binding is null)
            {
                // No matching tagHelpers, it's just HTML
                return false;
            }

            var boundAttributes = _tagHelperFactsService.GetBoundTagHelperAttributes(context.CodeDocument.GetTagHelperContext(), attribute.Name.GetContent(), binding);
            var isStringProperty = boundAttributes.FirstOrDefault(a => a.Name == attribute.Name.GetContent())?.IsStringProperty ?? true;

            return !isStringProperty;
        }
    }
}
