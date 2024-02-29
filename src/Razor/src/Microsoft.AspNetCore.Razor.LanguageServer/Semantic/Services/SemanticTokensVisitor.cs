﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic;

internal sealed class SemanticTokensVisitor : SyntaxWalker
{
    private readonly ImmutableArray<SemanticRange>.Builder _semanticRanges;
    private readonly RazorCodeDocument _razorCodeDocument;
    private readonly RazorSemanticTokensLegendService _razorSemanticTokensLegendService;
    private readonly bool _colorCodeBackground;

    private bool _addRazorCodeModifier;

    private SemanticTokensVisitor(ImmutableArray<SemanticRange>.Builder semanticRanges, RazorCodeDocument razorCodeDocument, TextSpan? range, RazorSemanticTokensLegendService razorSemanticTokensLegendService, bool colorCodeBackground)
        : base(range)
    {
        _semanticRanges = semanticRanges;
        _razorCodeDocument = razorCodeDocument;
        _razorSemanticTokensLegendService = razorSemanticTokensLegendService;
        _colorCodeBackground = colorCodeBackground;
    }

    public static ImmutableArray<SemanticRange> GetSemanticRanges(RazorCodeDocument razorCodeDocument, Range range, RazorSemanticTokensLegendService razorSemanticTokensLegendService, bool colorCodeBackground)
    {
        var sourceText = razorCodeDocument.GetSourceText();
        var rangeAsTextSpan = range.ToTextSpan(sourceText);

        using var _ = ArrayBuilderPool<SemanticRange>.GetPooledObject(out var builder);

        var visitor = new SemanticTokensVisitor(builder, razorCodeDocument, rangeAsTextSpan, razorSemanticTokensLegendService, colorCodeBackground);

        visitor.Visit(razorCodeDocument.GetSyntaxTree().Root);

        return builder.DrainToImmutable();
    }

    private void Visit(SyntaxList<RazorSyntaxNode> syntaxNodes)
    {
        for (var i = 0; i < syntaxNodes.Count; i++)
        {
            Visit(syntaxNodes[i]);
        }
    }

    #region HTML

    public override void VisitMarkupTextLiteral(MarkupTextLiteralSyntax node)
    {
        // Don't return anything for MarkupTextLiterals. It translates to "text" on the VS side, which is the default color anyway
    }

    public override void VisitMarkupLiteralAttributeValue(MarkupLiteralAttributeValueSyntax node)
    {
        AddSemanticRange(node, _razorSemanticTokensLegendService.TokenTypes.MarkupAttributeValue);
    }

    public override void VisitMarkupAttributeBlock(MarkupAttributeBlockSyntax node)
    {
        var tokenTypes = _razorSemanticTokensLegendService.TokenTypes;

        Visit(node.NamePrefix);
        AddSemanticRange(node.Name, tokenTypes.MarkupAttribute);
        Visit(node.NameSuffix);
        AddSemanticRange(node.EqualsToken, tokenTypes.MarkupOperator);

        AddSemanticRange(node.ValuePrefix, tokenTypes.MarkupAttributeQuote);
        Visit(node.Value);
        AddSemanticRange(node.ValueSuffix, tokenTypes.MarkupAttributeQuote);
    }

    public override void VisitMarkupStartTag(MarkupStartTagSyntax node)
    {
        var tokenTypes = _razorSemanticTokensLegendService.TokenTypes;

        if (node.IsMarkupTransition)
        {
            AddSemanticRange(node, tokenTypes.RazorDirective);
        }
        else
        {
            AddSemanticRange(node.OpenAngle, tokenTypes.MarkupTagDelimiter);
            if (node.Bang != null)
            {
                AddSemanticRange(node.Bang, tokenTypes.RazorTransition);
            }

            AddSemanticRange(node.Name, tokenTypes.MarkupElement);

            Visit(node.Attributes);
            if (node.ForwardSlash != null)
            {
                AddSemanticRange(node.ForwardSlash, tokenTypes.MarkupTagDelimiter);
            }

            AddSemanticRange(node.CloseAngle, tokenTypes.MarkupTagDelimiter);
        }
    }

    public override void VisitMarkupEndTag(MarkupEndTagSyntax node)
    {
        var tokenTypes = _razorSemanticTokensLegendService.TokenTypes;

        if (node.IsMarkupTransition)
        {
            AddSemanticRange(node, tokenTypes.RazorDirective);
        }
        else
        {
            AddSemanticRange(node.OpenAngle, tokenTypes.MarkupTagDelimiter);
            if (node.Bang != null)
            {
                AddSemanticRange(node.Bang, tokenTypes.RazorTransition);
            }

            if (node.ForwardSlash != null)
            {
                AddSemanticRange(node.ForwardSlash, tokenTypes.MarkupTagDelimiter);
            }

            AddSemanticRange(node.Name, tokenTypes.MarkupElement);

            AddSemanticRange(node.CloseAngle, tokenTypes.MarkupTagDelimiter);
        }
    }

    public override void VisitMarkupCommentBlock(MarkupCommentBlockSyntax node)
    {
        var tokenTypes = _razorSemanticTokensLegendService.TokenTypes;

        AddSemanticRange(node.Children[0], tokenTypes.MarkupCommentPunctuation);

        for (var i = 1; i < node.Children.Count - 1; i++)
        {
            var commentNode = node.Children[i];
            switch (commentNode.Kind)
            {
                case SyntaxKind.MarkupTextLiteral:
                    AddSemanticRange(commentNode, tokenTypes.MarkupComment);
                    break;
                default:
                    Visit(commentNode);
                    break;
            }
        }

        AddSemanticRange(node.Children[^1], tokenTypes.MarkupCommentPunctuation);
    }

    public override void VisitMarkupMinimizedAttributeBlock(MarkupMinimizedAttributeBlockSyntax node)
    {
        Visit(node.NamePrefix);
        AddSemanticRange(node.Name, _razorSemanticTokensLegendService.TokenTypes.MarkupAttribute);
    }

    #endregion HTML

    #region C#

    public override void VisitCSharpStatementBody(CSharpStatementBodySyntax node)
    {
        var tokenTypes = _razorSemanticTokensLegendService.TokenTypes;

        using (ColorCSharpBackground())
        {
            AddSemanticRange(node.OpenBrace, tokenTypes.RazorTransition);
        }

        Visit(node.CSharpCode);

        using (ColorCSharpBackground())
        {
            AddSemanticRange(node.CloseBrace, tokenTypes.RazorTransition);
        }
    }

    public override void VisitCSharpImplicitExpressionBody(CSharpImplicitExpressionBodySyntax node)
    {
        // Generally same as explicit expression, below, but different because the parens might not be there,
        // and because the compiler isn't nice and doesn't give us OpenParen and CloseParen properties we can
        // easily use.

        // Matches @(SomeCSharpCode())
        if (node.CSharpCode.Children is
            [
                CSharpExpressionLiteralSyntax { LiteralTokens: [{ Kind: SyntaxKind.LeftParenthesis } openParen] },
                CSharpExpressionLiteralSyntax body,
                CSharpExpressionLiteralSyntax { LiteralTokens: [{ Kind: SyntaxKind.RightParenthesis } closeParen] },
            ])
        {
            var tokenTypes = _razorSemanticTokensLegendService.TokenTypes;

            using (ColorCSharpBackground())
            {
                AddSemanticRange(openParen, tokenTypes.RazorTransition);
            }

            Visit(body);

            using (ColorCSharpBackground())
            {
                AddSemanticRange(closeParen, tokenTypes.RazorTransition);
            }
        }
        else
        {
            // Matches @SomeCSharpCode()
            Visit(node.CSharpCode);
        }
    }

    public override void VisitCSharpExplicitExpressionBody(CSharpExplicitExpressionBodySyntax node)
    {
        var tokenTypes = _razorSemanticTokensLegendService.TokenTypes;

        using (ColorCSharpBackground())
        {
            AddSemanticRange(node.OpenParen, tokenTypes.RazorTransition);
        }

        Visit(node.CSharpCode);

        using (ColorCSharpBackground())
        {
            AddSemanticRange(node.CloseParen, tokenTypes.RazorTransition);
        }
    }

    #endregion C#

    #region Razor

    public override void VisitRazorCommentBlock(RazorCommentBlockSyntax node)
    {
        var tokenTypes = _razorSemanticTokensLegendService.TokenTypes;

        AddSemanticRange(node.StartCommentTransition, tokenTypes.RazorCommentTransition);
        AddSemanticRange(node.StartCommentStar, tokenTypes.RazorCommentStar);
        AddSemanticRange(node.Comment, tokenTypes.RazorComment);
        AddSemanticRange(node.EndCommentStar, tokenTypes.RazorCommentStar);
        AddSemanticRange(node.EndCommentTransition, tokenTypes.RazorCommentTransition);
    }

    public override void VisitRazorMetaCode(RazorMetaCodeSyntax node)
    {
        if (node.Kind == SyntaxKind.RazorMetaCode)
        {
            AddSemanticRange(node, _razorSemanticTokensLegendService.TokenTypes.RazorTransition);
        }
        else
        {
            throw new NotSupportedException(SR.Unknown_RazorMetaCode);
        }
    }

    public override void VisitRazorDirectiveBody(RazorDirectiveBodySyntax node)
    {
        // We can't provide colors for CSharp because if we both provided them then they would overlap, which violates the LSP spec.
        if (node.Keyword.Kind != SyntaxKind.CSharpStatementLiteral)
        {
            AddSemanticRange(node.Keyword, _razorSemanticTokensLegendService.TokenTypes.RazorDirective);
        }
        else
        {
            Visit(node.Keyword);
        }

        Visit(node.CSharpCode);
    }

    public override void VisitMarkupTagHelperStartTag(MarkupTagHelperStartTagSyntax node)
    {
        var tokenTypes = _razorSemanticTokensLegendService.TokenTypes;

        AddSemanticRange(node.OpenAngle, tokenTypes.MarkupTagDelimiter);
        if (node.Bang != null)
        {
            AddSemanticRange(node.Bang, tokenTypes.RazorTransition);
        }

        if (ClassifyTagName((MarkupTagHelperElementSyntax)node.Parent))
        {
            var semanticKind = GetElementSemanticKind(node);
            AddSemanticRange(node.Name, semanticKind);
        }
        else
        {
            AddSemanticRange(node.Name, tokenTypes.MarkupElement);
        }

        Visit(node.Attributes);

        if (node.ForwardSlash != null)
        {
            AddSemanticRange(node.ForwardSlash, tokenTypes.MarkupTagDelimiter);
        }

        AddSemanticRange(node.CloseAngle, tokenTypes.MarkupTagDelimiter);
    }

    public override void VisitMarkupTagHelperEndTag(MarkupTagHelperEndTagSyntax node)
    {
        var tokenTypes = _razorSemanticTokensLegendService.TokenTypes;

        AddSemanticRange(node.OpenAngle, tokenTypes.MarkupTagDelimiter);
        AddSemanticRange(node.ForwardSlash, tokenTypes.MarkupTagDelimiter);

        if (node.Bang != null)
        {
            AddSemanticRange(node.Bang, tokenTypes.RazorTransition);
        }

        if (ClassifyTagName((MarkupTagHelperElementSyntax)node.Parent))
        {
            var semanticKind = GetElementSemanticKind(node);
            AddSemanticRange(node.Name, semanticKind);
        }
        else
        {
            AddSemanticRange(node.Name, tokenTypes.MarkupElement);
        }

        AddSemanticRange(node.CloseAngle, tokenTypes.MarkupTagDelimiter);
    }

    public override void VisitMarkupMinimizedTagHelperAttribute(MarkupMinimizedTagHelperAttributeSyntax node)
    {
        Visit(node.NamePrefix);

        if (node.TagHelperAttributeInfo.Bound)
        {
            var semanticKind = GetAttributeSemanticKind(node);
            AddSemanticRange(node.Name, semanticKind);
        }
        else
        {
            AddSemanticRange(node.Name, _razorSemanticTokensLegendService.TokenTypes.MarkupAttribute);
        }
    }

    public override void VisitMarkupTagHelperAttribute(MarkupTagHelperAttributeSyntax node)
    {
        var tokenTypes = _razorSemanticTokensLegendService.TokenTypes;

        Visit(node.NamePrefix);
        if (node.TagHelperAttributeInfo.Bound)
        {
            var semanticKind = GetAttributeSemanticKind(node);
            AddSemanticRange(node.Name, semanticKind);
        }
        else
        {
            AddSemanticRange(node.Name, tokenTypes.MarkupAttribute);
        }

        Visit(node.NameSuffix);

        AddSemanticRange(node.EqualsToken, tokenTypes.MarkupOperator);

        AddSemanticRange(node.ValuePrefix, tokenTypes.MarkupAttributeQuote);
        Visit(node.Value);
        AddSemanticRange(node.ValueSuffix, tokenTypes.MarkupAttributeQuote);
    }

    public override void VisitMarkupTagHelperAttributeValue(MarkupTagHelperAttributeValueSyntax node)
    {
        foreach (var child in node.Children)
        {
            if (child.Kind == SyntaxKind.MarkupTextLiteral)
            {
                AddSemanticRange(child, _razorSemanticTokensLegendService.TokenTypes.MarkupAttributeValue);
            }
            else
            {
                Visit(child);
            }
        }
    }

    public override void VisitMarkupTagHelperDirectiveAttribute(MarkupTagHelperDirectiveAttributeSyntax node)
    {
        var tokenTypes = _razorSemanticTokensLegendService.TokenTypes;

        if (node.TagHelperAttributeInfo.Bound)
        {
            Visit(node.Transition);
            Visit(node.NamePrefix);
            AddSemanticRange(node.Name, tokenTypes.RazorDirectiveAttribute);
            Visit(node.NameSuffix);

            if (node.Colon != null)
            {
                AddSemanticRange(node.Colon, tokenTypes.RazorDirectiveColon);
            }

            if (node.ParameterName != null)
            {
                AddSemanticRange(node.ParameterName, tokenTypes.RazorDirectiveAttribute);
            }
        }

        AddSemanticRange(node.EqualsToken, tokenTypes.MarkupOperator);
        AddSemanticRange(node.ValuePrefix, tokenTypes.MarkupAttributeQuote);
        Visit(node.Value);
        AddSemanticRange(node.ValueSuffix, tokenTypes.MarkupAttributeQuote);
    }

    public override void VisitMarkupMinimizedTagHelperDirectiveAttribute(MarkupMinimizedTagHelperDirectiveAttributeSyntax node)
    {
        var tokenTypes = _razorSemanticTokensLegendService.TokenTypes;

        if (node.TagHelperAttributeInfo.Bound)
        {
            AddSemanticRange(node.Transition, tokenTypes.RazorTransition);
            Visit(node.NamePrefix);
            AddSemanticRange(node.Name, tokenTypes.RazorDirectiveAttribute);

            if (node.Colon != null)
            {
                AddSemanticRange(node.Colon, tokenTypes.RazorDirectiveColon);
            }

            if (node.ParameterName != null)
            {
                AddSemanticRange(node.ParameterName, tokenTypes.RazorDirectiveAttribute);
            }
        }
    }

    public override void VisitCSharpTransition(CSharpTransitionSyntax node)
    {
        if (node.Parent is not RazorDirectiveSyntax)
        {
            using (ColorCSharpBackground())
            {
                AddSemanticRange(node, _razorSemanticTokensLegendService.TokenTypes.RazorTransition);
            }
        }
        else
        {
            AddSemanticRange(node, _razorSemanticTokensLegendService.TokenTypes.RazorTransition);
        }
    }

    public override void VisitMarkupTransition(MarkupTransitionSyntax node)
    {
        using (ColorCSharpBackground())
        {
            AddSemanticRange(node, _razorSemanticTokensLegendService.TokenTypes.RazorTransition);
        }
    }

    #endregion Razor

    private int GetElementSemanticKind(SyntaxNode node)
    {
        var tokenTypes = _razorSemanticTokensLegendService.TokenTypes;

        var semanticKind = IsComponent(node) ? tokenTypes.RazorComponentElement : tokenTypes.RazorTagHelperElement;
        return semanticKind;
    }

    private int GetAttributeSemanticKind(SyntaxNode node)
    {
        var tokenTypes = _razorSemanticTokensLegendService.TokenTypes;

        var semanticKind = IsComponent(node) ? tokenTypes.RazorComponentAttribute : tokenTypes.RazorTagHelperAttribute;
        return semanticKind;
    }

    private static bool IsComponent(SyntaxNode node)
    {
        if (node is MarkupTagHelperElementSyntax { TagHelperInfo.BindingResult: var binding })
        {
            var componentDescriptor = binding.Descriptors.FirstOrDefault(static d => d.IsComponentTagHelper);
            return componentDescriptor is not null;
        }
        else if (node is MarkupTagHelperStartTagSyntax startTag)
        {
            return IsComponent(startTag.Parent);
        }
        else if (node is MarkupTagHelperEndTagSyntax endTag)
        {
            return IsComponent(endTag.Parent);
        }
        else if (node is MarkupTagHelperAttributeSyntax attribute)
        {
            return IsComponent(attribute.Parent.Parent);
        }
        else if (node is MarkupMinimizedTagHelperAttributeSyntax minimizedTagHelperAttribute)
        {
            return IsComponent(minimizedTagHelperAttribute.Parent.Parent);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    // We don't want to classify TagNames of well-known HTML
    // elements as TagHelpers (even if they are). So the 'input' in`<input @onclick='...' />`
    // needs to not be marked as a TagHelper, but `<Input @onclick='...' />` should be.
    private static bool ClassifyTagName(MarkupTagHelperElementSyntax node)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (node.StartTag?.Name != null &&
            node.TagHelperInfo is { BindingResult: var binding })
        {
            return !binding.IsAttributeMatch;
        }

        return false;
    }

    private void AddSemanticRange(SyntaxNode node, int semanticKind)
    {
        if (node is null)
        {
            // This can happen in situations like "<p class='", where the trailing ' hasn't been typed yet.
            return;
        }

        if (node.Width == 0)
        {
            // Under no circumstances can we have 0-width spans.
            // This can happen in situations like "@* comment ", where EndCommentStar and EndCommentTransition are empty.
            return;
        }

        var source = _razorCodeDocument.Source;
        var range = node.GetLinePositionSpan(source);
        var tokenModifier = _addRazorCodeModifier ? _razorSemanticTokensLegendService.TokenModifiers.RazorCodeModifier : 0;

        // LSP spec forbids multi-line tokens, so we need to split this up.
        if (range.Start.Line != range.End.Line)
        {
            var childNodes = node.ChildNodes();
            if (childNodes.Count == 0)
            {
                var charPosition = range.Start.Character;
                var lineStartAbsoluteIndex = node.SpanStart - charPosition;
                for (var lineNumber = range.Start.Line; lineNumber <= range.End.Line; lineNumber++)
                {
                    var originalCharPosition = charPosition;
                    // NOTE: We don't report tokens for newlines so need to account for them.
                    var lineLength = source.Text.Lines[lineNumber].SpanIncludingLineBreak.Length;

                    // For the last line, we end where the syntax tree tells us to. For all other lines, we end at the
                    // last non-newline character
                    var endChar = lineNumber == range.End.Line
                       ? range.End.Character
                       : GetLastNonWhitespaceCharacterOffset(source, lineStartAbsoluteIndex, lineLength);

                    // Make sure we move our line start index pointer on, before potentially breaking out of the loop
                    lineStartAbsoluteIndex += lineLength;
                    charPosition = 0;

                    // No tokens for blank lines
                    if (endChar == 0)
                    {
                        continue;
                    }

                    var semantic = new SemanticRange(semanticKind, lineNumber, originalCharPosition, lineNumber, endChar, tokenModifier, fromRazor: true);
                    AddRange(semantic);
                }
            }
            else
            {
                // We have to iterate over the individual nodes because this node might consist of multiple lines
                // ie: "\r\ntext\r\n" would be parsed as one node containing three elements (newline, "text", newline).
                foreach (var token in node.ChildNodes())
                {
                    // We skip whitespace to avoid "multiline" ranges for "/r/n", where the /n is interpreted as being on a new line.
                    // This also stops us from returning data for " ", which seems like a nice side-effect as it's not likely to have any colorization anyway.
                    if (!token.ContainsOnlyWhitespace())
                    {
                        var lineSpan = token.GetLinePositionSpan(source);
                        var semantic = new SemanticRange(semanticKind, lineSpan.Start.Line, lineSpan.Start.Character, lineSpan.End.Line, lineSpan.End.Character, tokenModifier, fromRazor: true);
                        AddRange(semantic);
                    }
                }
            }
        }
        else
        {
            var semanticRange = new SemanticRange(semanticKind, range.Start.Line, range.Start.Character, range.End.Line, range.End.Character, tokenModifier, fromRazor: true);
            AddRange(semanticRange);
        }

        void AddRange(SemanticRange semanticRange)
        {
            // If the end is before the start, well that's no good!
            if (semanticRange.EndLine < semanticRange.StartLine)
            {
                return;
            }

            // If the end is before the start, that's still no good, but I'm separating out this check
            // to make it clear that it also checks for equality: No point classifying 0-length ranges.
            if (semanticRange.EndLine == semanticRange.StartLine &&
                semanticRange.EndCharacter <= semanticRange.StartCharacter)
            {
                return;
            }

            _semanticRanges.Add(semanticRange);
        }

        static int GetLastNonWhitespaceCharacterOffset(RazorSourceDocument source, int lineStartAbsoluteIndex, int lineLength)
        {
            // lineStartAbsoluteIndex + lineLength is the first character of the next line, so move back one to get to the end of the line
            lineLength--;

            var lineEndAbsoluteIndex = lineStartAbsoluteIndex + lineLength;
            if (lineEndAbsoluteIndex == 0 || lineLength == 0)
            {
                return lineLength;
            }

            return source.Text[lineEndAbsoluteIndex - 1] is '\n' or '\r'
                ? lineLength - 1
                : lineLength;
        }
    }

    private BackgroundColorDisposable ColorCSharpBackground()
    {
        return new BackgroundColorDisposable(this);
    }

    private readonly struct BackgroundColorDisposable : IDisposable
    {
        private readonly SemanticTokensVisitor _visitor;

        public BackgroundColorDisposable(SemanticTokensVisitor tagHelperSemanticRangeVisitor)
        {
            _visitor = tagHelperSemanticRangeVisitor;

            _visitor._addRazorCodeModifier = _visitor._colorCodeBackground;
        }

        public void Dispose()
        {
            _visitor._addRazorCodeModifier = false;
        }
    }
}
