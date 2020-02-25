// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.Language
{
    internal class RazorHtmlLoweringPass : IntermediateNodePassBase, IRazorDocumentClassifierPass
    {
        protected override void ExecuteCore(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
        {
            if (!documentNode.Options.DesignTime)
            {
                // Not needed in run time. This pass generates the backing HTML document that is used to provide HTML intellisense.
                return;
            }

            var generatedHtml = HtmlWriter.GetGeneratedHtml(codeDocument);
            var razorHtmlDocument = new DefaultRazorHtmlDocument(generatedHtml, documentNode.Options);
            codeDocument.SetHtmlDocument(razorHtmlDocument);
        }

        // We want to generate a HTML document that contains only pure HTML.
        // So we want replace all non-HTML content with whitespace.
        // Ideally we should just use ClassifiedSpans to generate this document but
        // not all characters in the document are included in the ClassifiedSpans.
        internal class HtmlWriter : SyntaxWalker
        {
            private bool _isHtml;

            private HtmlWriter(RazorSourceDocument source)
            {
                if (source is null)
                {
                    throw new ArgumentNullException(nameof(source));
                }

                Source = source;
                Builder = new StringBuilder(Source.Length);
                _isHtml = true;
            }

            public RazorSourceDocument Source { get; }

            public StringBuilder Builder { get; }

            public static string GetGeneratedHtml(RazorCodeDocument codeDocument)
            {
                var writer = new HtmlWriter(codeDocument.Source);
                var syntaxTree = codeDocument.GetSyntaxTree();

                writer.Visit(syntaxTree.Root);

                var generatedHtml = writer.Builder.ToString();
                Debug.Assert(
                    writer.Source.Length == writer.Builder.Length,
                    $"The backing HTML document should be the same length as the original document. Expected: {writer.Source.Length} Actual: {writer.Builder.Length}");

                return generatedHtml;
            }

            public override void VisitRazorCommentBlock(RazorCommentBlockSyntax node)
            {
                WriteNode(node, isHtml: false, base.VisitRazorCommentBlock);
            }

            public override void VisitRazorMetaCode(RazorMetaCodeSyntax node)
            {
                WriteNode(node, isHtml: false, base.VisitRazorMetaCode);
            }

            public override void VisitMarkupTransition(MarkupTransitionSyntax node)
            {
                WriteNode(node, isHtml: false, base.VisitMarkupTransition);
            }

            public override void VisitCSharpTransition(CSharpTransitionSyntax node)
            {
                WriteNode(node, isHtml: false, base.VisitCSharpTransition);
            }

            public override void VisitCSharpEphemeralTextLiteral(CSharpEphemeralTextLiteralSyntax node)
            {
                WriteNode(node, isHtml: false, base.VisitCSharpEphemeralTextLiteral);
            }

            public override void VisitCSharpExpressionLiteral(CSharpExpressionLiteralSyntax node)
            {
                WriteNode(node, isHtml: false, base.VisitCSharpExpressionLiteral);
            }

            public override void VisitCSharpStatementLiteral(CSharpStatementLiteralSyntax node)
            {
                WriteNode(node, isHtml: false, base.VisitCSharpStatementLiteral);
            }

            public override void VisitMarkupStartTag(MarkupStartTagSyntax node)
            {
                WriteNode(node, isHtml: true, base.VisitMarkupStartTag);
            }

            public override void VisitMarkupEndTag(MarkupEndTagSyntax node)
            {
                WriteNode(node, isHtml: true, base.VisitMarkupEndTag);
            }

            public override void VisitMarkupTagHelperStartTag(MarkupTagHelperStartTagSyntax node)
            {
                WriteNode(node, isHtml: true, base.VisitMarkupTagHelperStartTag);
            }

            public override void VisitMarkupTagHelperEndTag(MarkupTagHelperEndTagSyntax node)
            {
                WriteNode(node, isHtml: true, base.VisitMarkupTagHelperEndTag);
            }

            public override void VisitMarkupEphemeralTextLiteral(MarkupEphemeralTextLiteralSyntax node)
            {
                WriteNode(node, isHtml: true, base.VisitMarkupEphemeralTextLiteral);
            }

            public override void VisitMarkupTextLiteral(MarkupTextLiteralSyntax node)
            {
                WriteNode(node, isHtml: true, base.VisitMarkupTextLiteral);
            }

            public override void VisitUnclassifiedTextLiteral(UnclassifiedTextLiteralSyntax node)
            {
                WriteNode(node, isHtml: true, base.VisitUnclassifiedTextLiteral);
            }

            public override void VisitToken(SyntaxToken token)
            {
                base.VisitToken(token);
                WriteToken(token);
            }

            private void WriteToken(SyntaxToken token)
            {
                var content = token.Content;
                if (_isHtml)
                {
                    // If we're in HTML context, append the content directly.
                    Builder.Append(content);
                    return;
                }

                // We're in non-HTML context. Let's replace all non-whitespace chars with a space.
                foreach (var c in content)
                {
                    if (char.IsWhiteSpace(c))
                    {
                        Builder.Append(c);
                    }
                    else
                    {
                        Builder.Append(' ');
                    }
                }
            }

            private void WriteNode<TNode>(TNode node, bool isHtml, Action<TNode> handler) where TNode : SyntaxNode
            {
                var old = _isHtml;
                _isHtml = isHtml;
                handler(node);
                _isHtml = old;
            }
        }
    }
}
