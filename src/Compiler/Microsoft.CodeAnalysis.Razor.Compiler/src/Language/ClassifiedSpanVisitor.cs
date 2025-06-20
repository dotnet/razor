// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.Language;

internal class ClassifiedSpanVisitor : SyntaxWalker
{
    private static readonly ObjectPool<ImmutableArray<ClassifiedSpanInternal>.Builder> Pool = DefaultPool.Create(Policy.Instance, size: 5);

    private readonly RazorSourceDocument _source;
    private readonly ImmutableArray<ClassifiedSpanInternal>.Builder _spans;

    private SyntaxNode? _currentBlock;
    private SourceSpan? _currentBlockSpan;
    private BlockKindInternal _currentBlockKind;

    private ClassifiedSpanVisitor(RazorSourceDocument source, ImmutableArray<ClassifiedSpanInternal>.Builder spans)
    {
        _source = source;
        _spans = spans;

        _currentBlockKind = BlockKindInternal.Markup;
    }

    public static ImmutableArray<ClassifiedSpanInternal> VisitRoot(RazorSyntaxTree syntaxTree)
    {
        using var _ = Pool.GetPooledObject(out var builder);

        var visitor = new ClassifiedSpanVisitor(syntaxTree.Source, builder);
        visitor.Visit(syntaxTree.Root);

        return builder.ToImmutableAndClear();
    }

    public override void VisitRazorCommentBlock(RazorCommentBlockSyntax node)
    {
        using (CommentBlock(node))
        {
            AddSpan(node.StartCommentTransition, SpanKindInternal.Transition, AcceptedCharactersInternal.None);
            AddSpan(node.StartCommentStar, SpanKindInternal.MetaCode, AcceptedCharactersInternal.None);

            var comment = node.Comment;

            if (comment.IsMissing)
            {
                // We need to generate a classified span at this position. So insert a marker in its place.
                comment = SyntaxFactory.Token(SyntaxKind.Marker, parent: node, position: node.StartCommentStar.EndPosition);
            }

            AddSpan(comment, SpanKindInternal.Comment, AcceptedCharactersInternal.Any);

            AddSpan(node.EndCommentStar, SpanKindInternal.MetaCode, AcceptedCharactersInternal.None);
            AddSpan(node.EndCommentTransition, SpanKindInternal.Transition, AcceptedCharactersInternal.None);
        }
    }

    public override void VisitCSharpCodeBlock(CSharpCodeBlockSyntax node)
    {
        if (node.Parent is CSharpStatementBodySyntax or
                           CSharpExplicitExpressionBodySyntax or
                           CSharpImplicitExpressionBodySyntax or
                           RazorDirectiveBodySyntax ||
            (_currentBlockKind == BlockKindInternal.Directive && node.Children is [CSharpStatementLiteralSyntax]))
        {
            base.VisitCSharpCodeBlock(node);
            return;
        }

        using (StatementBlock(node))
        {
            base.VisitCSharpCodeBlock(node);
        }
    }

    public override void VisitCSharpStatement(CSharpStatementSyntax node)
    {
        using (StatementBlock(node))
        {
            base.VisitCSharpStatement(node);
        }
    }

    public override void VisitCSharpExplicitExpression(CSharpExplicitExpressionSyntax node)
    {
        using (ExpressionBlock(node))
        {
            base.VisitCSharpExplicitExpression(node);
        }
    }

    public override void VisitCSharpImplicitExpression(CSharpImplicitExpressionSyntax node)
    {
        using (ExpressionBlock(node))
        {
            base.VisitCSharpImplicitExpression(node);
        }
    }

    public override void VisitRazorDirective(RazorDirectiveSyntax node)
    {
        using (DirectiveBlock(node))
        {
            base.VisitRazorDirective(node);
        }
    }

    public override void VisitCSharpTemplateBlock(CSharpTemplateBlockSyntax node)
    {
        using (TemplateBlock(node))
        {
            base.VisitCSharpTemplateBlock(node);
        }
    }

    public override void VisitMarkupBlock(MarkupBlockSyntax node)
    {
        using (MarkupBlock(node))
        {
            base.VisitMarkupBlock(node);
        }
    }

    public override void VisitMarkupTagHelperAttributeValue(MarkupTagHelperAttributeValueSyntax node)
    {
        // We don't generate a classified span when the attribute value is a simple literal value.
        // This is done so we maintain the classified spans generated in 2.x which
        // used ConditionalAttributeCollapser (combines markup literal attribute values into one span with no block parent).
        if (!IsSimpleLiteralValue(node))
        {
            base.VisitMarkupTagHelperAttributeValue(node);
            return;
        }

        using (MarkupBlock(node))
        {
            base.VisitMarkupTagHelperAttributeValue(node);
        }

        static bool IsSimpleLiteralValue(MarkupTagHelperAttributeValueSyntax node)
        {
            return node.Children is [MarkupDynamicAttributeValueSyntax] or { Count: > 1 };
        }
    }

    public override void VisitMarkupStartTag(MarkupStartTagSyntax node)
    {
        using (TagBlock(node))
        {
            var children = SyntaxUtilities.GetRewrittenMarkupStartTagChildren(node, includeEditHandler: true);
            foreach (var child in children)
            {
                Visit(child);
            }
        }
    }

    public override void VisitMarkupEndTag(MarkupEndTagSyntax node)
    {
        using (TagBlock(node))
        {
            var children = SyntaxUtilities.GetRewrittenMarkupEndTagChildren(node, includeEditHandler: true);

            foreach (var child in children)
            {
                Visit(child);
            }
        }
    }

    public override void VisitMarkupTagHelperElement(MarkupTagHelperElementSyntax node)
    {
        using (TagBlock(node))
        {
            base.VisitMarkupTagHelperElement(node);
        }
    }

    public override void VisitMarkupTagHelperStartTag(MarkupTagHelperStartTagSyntax node)
    {
        foreach (var child in node.Attributes)
        {
            if (child is MarkupTagHelperAttributeSyntax or
                         MarkupTagHelperDirectiveAttributeSyntax or
                         MarkupMinimizedTagHelperDirectiveAttributeSyntax)
            {
                Visit(child);
            }
        }
    }

    public override void VisitMarkupTagHelperEndTag(MarkupTagHelperEndTagSyntax node)
    {
        // We don't want to generate a classified span for a tag helper end tag. Do nothing.
    }

    public override void VisitMarkupAttributeBlock(MarkupAttributeBlockSyntax node)
    {
        using (MarkupBlock(node))
        {
            var equalsSyntax = SyntaxFactory.MarkupTextLiteral(new SyntaxTokenList(node.EqualsToken), chunkGenerator: null);
            var mergedAttributePrefix = SyntaxUtilities.MergeTextLiterals(node.NamePrefix, node.Name, node.NameSuffix, equalsSyntax, node.ValuePrefix);

            Visit(mergedAttributePrefix);
            Visit(node.Value);
            Visit(node.ValueSuffix);
        }
    }

    public override void VisitMarkupTagHelperAttribute(MarkupTagHelperAttributeSyntax node)
    {
        Visit(node.Value);
    }

    public override void VisitMarkupTagHelperDirectiveAttribute(MarkupTagHelperDirectiveAttributeSyntax node)
    {
        Visit(node.Transition);
        Visit(node.Colon);
        Visit(node.Value);
    }

    public override void VisitMarkupMinimizedTagHelperDirectiveAttribute(MarkupMinimizedTagHelperDirectiveAttributeSyntax node)
    {
        Visit(node.Transition);
        Visit(node.Colon);
    }

    public override void VisitMarkupMinimizedAttributeBlock(MarkupMinimizedAttributeBlockSyntax node)
    {
        using (MarkupBlock(node))
        {
            var mergedAttributePrefix = SyntaxUtilities.MergeTextLiterals(node.NamePrefix, node.Name);
            Visit(mergedAttributePrefix);
        }
    }

    public override void VisitMarkupCommentBlock(MarkupCommentBlockSyntax node)
    {
        using (HtmlCommentBlock(node))
        {
            base.VisitMarkupCommentBlock(node);
        }
    }

    public override void VisitMarkupDynamicAttributeValue(MarkupDynamicAttributeValueSyntax node)
    {
        using (MarkupBlock(node))
        {
            base.VisitMarkupDynamicAttributeValue(node);
        }
    }

    public override void VisitRazorMetaCode(RazorMetaCodeSyntax node)
    {
        AddSpan(node, SpanKindInternal.MetaCode);
        base.VisitRazorMetaCode(node);
    }

    public override void VisitCSharpTransition(CSharpTransitionSyntax node)
    {
        AddSpan(node, SpanKindInternal.Transition);
        base.VisitCSharpTransition(node);
    }

    public override void VisitMarkupTransition(MarkupTransitionSyntax node)
    {
        AddSpan(node, SpanKindInternal.Transition);
        base.VisitMarkupTransition(node);
    }

    public override void VisitCSharpStatementLiteral(CSharpStatementLiteralSyntax node)
    {
        AddSpan(node, SpanKindInternal.Code);
        base.VisitCSharpStatementLiteral(node);
    }

    public override void VisitCSharpExpressionLiteral(CSharpExpressionLiteralSyntax node)
    {
        AddSpan(node, SpanKindInternal.Code);
        base.VisitCSharpExpressionLiteral(node);
    }

    public override void VisitCSharpEphemeralTextLiteral(CSharpEphemeralTextLiteralSyntax node)
    {
        AddSpan(node, SpanKindInternal.Code);
        base.VisitCSharpEphemeralTextLiteral(node);
    }

    public override void VisitUnclassifiedTextLiteral(UnclassifiedTextLiteralSyntax node)
    {
        AddSpan(node, SpanKindInternal.None);
        base.VisitUnclassifiedTextLiteral(node);
    }

    public override void VisitMarkupLiteralAttributeValue(MarkupLiteralAttributeValueSyntax node)
    {
        AddSpan(node, SpanKindInternal.Markup);
        base.VisitMarkupLiteralAttributeValue(node);
    }

    public override void VisitMarkupTextLiteral(MarkupTextLiteralSyntax node)
    {
        if (node.Parent is MarkupLiteralAttributeValueSyntax)
        {
            base.VisitMarkupTextLiteral(node);
            return;
        }

        AddSpan(node, SpanKindInternal.Markup);
        base.VisitMarkupTextLiteral(node);
    }

    public override void VisitMarkupEphemeralTextLiteral(MarkupEphemeralTextLiteralSyntax node)
    {
        AddSpan(node, SpanKindInternal.Markup);
        base.VisitMarkupEphemeralTextLiteral(node);
    }

    private BlockSaver CommentBlock(SyntaxNode node)
        => Block(node, BlockKindInternal.Comment);

    private BlockSaver DirectiveBlock(SyntaxNode node)
        => Block(node, BlockKindInternal.Directive);

    private BlockSaver ExpressionBlock(SyntaxNode node)
        => Block(node, BlockKindInternal.Expression);

    private BlockSaver HtmlCommentBlock(SyntaxNode node)
        => Block(node, BlockKindInternal.HtmlComment);

    private BlockSaver MarkupBlock(SyntaxNode node)
        => Block(node, BlockKindInternal.Markup);

    private BlockSaver StatementBlock(SyntaxNode node)
        => Block(node, BlockKindInternal.Statement);

    private BlockSaver TagBlock(SyntaxNode node)
        => Block(node, BlockKindInternal.Tag);

    private BlockSaver TemplateBlock(SyntaxNode node)
        => Block(node, BlockKindInternal.Template);

    private BlockSaver Block(SyntaxNode node, BlockKindInternal kind)
    {
        var saver = new BlockSaver(this);

        _currentBlock = node;
        _currentBlockKind = kind;

        // This is a new block, so we reset the current block span.
        // It will be computed when the first span is written.
        _currentBlockSpan = null;

        return saver;
    }

    private readonly ref struct BlockSaver(ClassifiedSpanVisitor visitor)
    {
        private readonly SyntaxNode? _previousBlock = visitor._currentBlock;
        private readonly SourceSpan? _previousBlockSpan = visitor._currentBlockSpan;
        private readonly BlockKindInternal _previousKind = visitor._currentBlockKind;

        public void Dispose()
        {
            visitor._currentBlock = _previousBlock;
            visitor._currentBlockSpan = _previousBlockSpan;
            visitor._currentBlockKind = _previousKind;
        }
    }

    private SourceSpan CurrentBlockSpan
        => _currentBlockSpan ??= _currentBlock.AssumeNotNull().GetSourceSpan(_source);

    private void AddSpan(SyntaxNode node, SpanKindInternal kind)
    {
        if (node.IsMissing)
        {
            return;
        }

        Debug.Assert(_currentBlock != null, "Current block should not be null when writing a span for a node.");

        var nodeSpan = node.GetSourceSpan(_source);

        var acceptedCharacters = node.GetEditHandler() is { } context
            ? context.AcceptedCharacters
            : AcceptedCharactersInternal.Any;

        AddSpan(nodeSpan, kind, acceptedCharacters);
    }

    private void AddSpan(SyntaxToken token, SpanKindInternal kind, AcceptedCharactersInternal acceptedCharacters)
    {
        if (token.IsMissing)
        {
            return;
        }

        Debug.Assert(_currentBlock != null, "Current block should not be null when writing a span for a token.");

        var tokenSpan = token.GetSourceSpan(_source);

        AddSpan(tokenSpan, kind, acceptedCharacters);
    }

    private void AddSpan(SourceSpan span, SpanKindInternal kind, AcceptedCharactersInternal acceptedCharacters)
        => _spans.Add(new(span, CurrentBlockSpan, kind, _currentBlockKind, acceptedCharacters));

    private sealed class Policy : IPooledObjectPolicy<ImmutableArray<ClassifiedSpanInternal>.Builder>
    {
        public static readonly Policy Instance = new();

        // Significantly larger than DefaultPool.MaximumObjectSize as there shouldn't be much concurrency
        // of these arrays (we limit the number of pooled items to 5) and they are commonly large
        public const int MaximumObjectSize = DefaultPool.MaximumObjectSize * 32;

        private Policy()
        {
        }

        public ImmutableArray<ClassifiedSpanInternal>.Builder Create() => ImmutableArray.CreateBuilder<ClassifiedSpanInternal>();

        public bool Return(ImmutableArray<ClassifiedSpanInternal>.Builder builder)
        {
            builder.Clear();

            if (builder.Capacity > MaximumObjectSize)
            {
                // Differs from ArrayBuilderPool.Policy's behavior as we allow our array to grow significantly larger
                builder.Capacity = 0;
            }

            return true;
        }
    }
}
