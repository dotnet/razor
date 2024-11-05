// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.ObjectPool;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using static Microsoft.AspNetCore.Razor.LanguageServer.Mapping.RazorMapToDocumentEditsEndpoint;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Mapping;

internal static partial class RazorEditHelper
{
    private class TextChangeBuilder(IDocumentMappingService documentMappingService) : IDisposable
    {
        private static readonly ObjectPool<ImmutableArray<TextChange>.Builder> Pool = ArrayBuilderPool<TextChange>.Default;
        private readonly ImmutableArray<TextChange>.Builder _builder = Pool.Get();
        private readonly IDocumentMappingService _documentMappingService = documentMappingService;

        public void Dispose()
        {
            Pool.Return(_builder);
        }

        public ImmutableArray<TextChange> DrainToOrderedImmutable()
            => _builder.DrainToImmutableOrderedBy(e => e.Span.Start);

        /// <summary>
        /// For all edits that are not mapped to using directives, add them directly to the builder.
        /// Edits that are not mapped are skipped, and using directive changes are handled by <see cref="AddUsingsChanges(RazorCodeDocument, ImmutableArray{string}, ImmutableArray{string}, CancellationToken)"/>
        /// </summary>
        public void AddDirectlyMappedChanges(ImmutableArray<TextChange> edits, RazorCodeDocument codeDocument, CancellationToken cancellationToken)
        {
            var root = codeDocument.GetSyntaxTree().Root;
            var razorText = codeDocument.Source.Text;
            var csharpDocument = codeDocument.GetCSharpDocument();
            var csharpText = csharpDocument.GetGeneratedSourceText();
            foreach (var edit in edits)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var linePositionSpan = csharpText.GetLinePositionSpan(edit.Span);

                if (!_documentMappingService.TryMapToHostDocumentRange(
                    csharpDocument,
                    linePositionSpan,
                    MappingBehavior.Strict,
                    out var mappedLinePositionSpan))
                {
                    continue;
                }

                var mappedSpan = razorText.GetTextSpan(mappedLinePositionSpan);
                var node = root.FindNode(mappedSpan);
                if (node is null)
                {
                    continue;
                }

                if (RazorSyntaxFacts.IsInUsingDirective(node))
                {
                    continue;
                }

                var mappedEdit = new TextChange(mappedSpan, edit.NewText ?? "");
                _builder.Add(mappedEdit);
            }
        }

        /// <summary>
        /// Given a set of new and removed usings, adds text changes to this builder using the following logic:
        ///
        /// <list type="number">
        /// <item>
        /// If there are no existing usings the new usings are added at the top of the document following any page, component, or namespace directives.
        /// </item>
        ///
        /// <item>
        /// If there are existing usings but they are in a continous block, replace that block with the new ordered set of usings.
        /// </item>
        ///
        /// <item>
        /// If for some reason a user has usings not in a single block (allows for whitespace), then replace the first block of using directives
        /// with the set of usings within that block that have not been removed AND the new usings. The remaining directives outside the block are removed
        /// as needed.
        /// </item>
        /// </list>
        /// </summary>
        public void AddUsingsChanges(
            RazorCodeDocument codeDocument,
            ImmutableArray<string> addedUsings,
            ImmutableArray<string> removedUsings,
            CancellationToken cancellationToken)
        {
            if (addedUsings.Length == 0 && removedUsings.Length == 0)
            {
                return;
            }

            // If only usings were added then just need to find where to insert them.
            if (removedUsings.Length == 0)
            {
                AddNewUsingChanges(codeDocument, addedUsings, cancellationToken);
                return;
            }

            // If only usings are being removed complex logic can be avoided
            if (addedUsings.Length == 0)
            {
                AddRemoveUsingsChanges(codeDocument, removedUsings, cancellationToken);
                return;
            }

            AddComplexUsingsChanges(codeDocument, addedUsings, removedUsings, cancellationToken);
        }

        private void AddNewUsingChanges(RazorCodeDocument codeDocument, ImmutableArray<string> addedUsings, CancellationToken cancellationToken)
        {
            var existingUsings = GetUsingsNodes(codeDocument, cancellationToken);

            // If no usings are present then simply add all the usings as a block
            if (existingUsings.Length == 0)
            {
                var span = FindFirstTopLevelSpotForUsing(codeDocument);
                var newText = GetUsingsText(usingDirectives: [], addedUsings, removedUsings: []);
                _builder.Add(new TextChange(span, newText));

                return;
            }

            // If usings are already present, find where to add new ones
            // relative to existing ones
            var sortedExistingUsings = existingUsings.OrderAsArray(UsingsNodeComparer.Instance);
            var sortedAddedUsings = addedUsings.OrderAsArray(UsingsStringComparer.Instance);
            var indexToEditsMap = new Dictionary<int, string>();
            foreach (var newUsing in sortedAddedUsings)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var absoluteIndex = GetInsertionPointNextToExistingUsing(sortedExistingUsings, newUsing, codeDocument);

                indexToEditsMap.TryGetValue(absoluteIndex, out var newText);
                indexToEditsMap[absoluteIndex] = newText + GetUsingsText(newUsing);
            }

            // Finally, add all the edits for the new usings text
            foreach (var (index, newText) in indexToEditsMap)
            {
                var span = new TextSpan(index, 0);
                _builder.Add(new TextChange(span, newText));
            }

            static TextSpan FindFirstTopLevelSpotForUsing(RazorCodeDocument codeDocument)
            {
                var root = codeDocument.GetSyntaxTree().Root;
                var nodeToInsertAfter = root
                    .DescendantNodes()
                    .LastOrDefault(t => t is RazorDirectiveSyntax razorDirectiveSyntax
                    && (razorDirectiveSyntax.DirectiveDescriptor == ComponentPageDirective.Directive
                        || razorDirectiveSyntax.DirectiveDescriptor == NamespaceDirective.Directive
                        || razorDirectiveSyntax.DirectiveDescriptor == PageDirective.Directive));

                if (nodeToInsertAfter is null)
                {
                    return new TextSpan();
                }

                var start = nodeToInsertAfter.Span.End;
                return new TextSpan(start, 0);
            }

            static int GetInsertionPointNextToExistingUsing(ImmutableArray<RazorDirectiveSyntax> existingUsings, string newUsing, RazorCodeDocument codeDocument)
            {
                Debug.Assert(existingUsings.Length > 0);

                for (var i = 0; i < existingUsings.Length; i++)
                {
                    var directive = existingUsings[i];
                    RazorSyntaxFacts.TryGetNamespaceFromDirective(directive, out var @namespace);
                    var comparedValue = UsingsStringComparer.Instance.Compare(@namespace, newUsing);

                    // New using goes before existing using
                    if (comparedValue >= 0)
                    {
                        return directive.Span.Start;
                    }
                    // Last using and the new using goes after it
                    else if (i == (existingUsings.Length - 1) && comparedValue < 0)
                    {
                        return AdjustPositionToEndOfLine(directive.Span.End, codeDocument.Source.Text);
                    }
                }

                return existingUsings[0].Span.Start;
            }
        }

        private void AddRemoveUsingsChanges(RazorCodeDocument codeDocument, ImmutableArray<string> removedUsings, CancellationToken cancellationToken)
        {
            var allUsingNodes = GetUsingsNodes(codeDocument, cancellationToken);
            foreach (var node in allUsingNodes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                RazorSyntaxFacts.TryGetNamespaceFromDirective(node, out var @namespace);
                Debug.Assert(@namespace is not null);
                if (removedUsings.Contains(@namespace))
                {
                    AddRemoveEdit(node, codeDocument.Source.Text);
                }
            }
        }

        private void AddComplexUsingsChanges(
            RazorCodeDocument codeDocument,
            ImmutableArray<string> addedUsings,
            ImmutableArray<string> removedUsings,
            CancellationToken cancellationToken)
        {
            Debug.Assert(addedUsings.Length > 0, "There should be at least one added using for complex changes");
            Debug.Assert(removedUsings.Length > 0, "There should be at least one removed using for complex changes");

            var allUsingNodes = GetUsingsNodes(codeDocument, cancellationToken);

            AddUsingsChangesWithNodes(
                    codeDocument,
                    allUsingNodes,
                    addedUsings,
                    removedUsings,
                    cancellationToken);
        }

        private static ImmutableArray<RazorDirectiveSyntax> GetUsingsNodes(RazorCodeDocument codeDocument, CancellationToken cancellationToken)
        {
            var syntaxTreeRoot = codeDocument.GetSyntaxTree().Root;
            using var usingsNodesBuilder = new PooledArrayBuilder<RazorDirectiveSyntax>();

            foreach (var node in syntaxTreeRoot.DescendantNodes())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (node is RazorDirectiveSyntax razorDirective
                    && RazorSyntaxFacts.IsInUsingDirective(razorDirective))
                {
                    usingsNodesBuilder.Add(razorDirective);
                }
            }

            return usingsNodesBuilder.DrainToImmutable();
        }

        private void AddUsingsChangesWithNodes(
            RazorCodeDocument codeDocument,
            ImmutableArray<RazorDirectiveSyntax> allUsingNodes,
            ImmutableArray<string> newUsings,
            ImmutableArray<string> removedUsings,
            CancellationToken cancellationToken)
        {
            var startNode = allUsingNodes[0];
            var endNode = startNode;

            // It's not guaranteed that usings are continuous so this code has to account for that.
            // The logic is as follows:
            // All usings that are in a continuous block are bulk replaced with the set containing them and the new using directives.
            // All usings outside of the continous block are checked to see if they need to be removed
            using var nonContinuousUsingsToRemove = new PooledArrayBuilder<RazorDirectiveSyntax>(allUsingNodes.Length);
            using var usingsNodesBuilder = new PooledArrayBuilder<RazorDirectiveSyntax>(allUsingNodes.Length);
            usingsNodesBuilder.Add(startNode);
            var allUsingsInContinuousBlock = true;

            foreach (var node in allUsingNodes.Skip(1))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (allUsingsInContinuousBlock &&
                    RazorSyntaxFacts.AreNextToEachother(startNode, node, codeDocument.Source.Text))
                {
                    endNode = node;
                    usingsNodesBuilder.Add(node);
                }
                else
                {
                    allUsingsInContinuousBlock = false;
                    if (RazorSyntaxFacts.TryGetNamespaceFromDirective(node, out var @namespace)
                        && removedUsings.Contains(@namespace))
                    {
                        nonContinuousUsingsToRemove.Add(node);
                    }
                }
            }

            var startPosition = startNode.Span.Start;
            var endPosition = endNode.Span.End;

            endPosition = AdjustPositionToEndOfLine(endPosition, codeDocument.Source.Text);

            var span = TextSpan.FromBounds(startPosition, endPosition);
            var newText = GetUsingsText(usingsNodesBuilder.ToImmutable(), newUsings, removedUsings);
            _builder.Add(new TextChange(span, newText));

            foreach (var node in nonContinuousUsingsToRemove)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddRemoveEdit(node, codeDocument.Source.Text);
            }
        }

        private void AddRemoveEdit(RazorDirectiveSyntax node, SourceText text)
        {
            var start = node.Span.Start;
            var end = AdjustPositionToEndOfLine(node.Span.End, text);
            var removeSpan = TextSpan.FromBounds(start, end);
            _builder.Add(new TextChange(removeSpan, ""));
        }

        private static int AdjustPositionToEndOfLine(int endPosition, SourceText text)
        {
            if (endPosition >= text.Length)
            {
                return endPosition;
            }

            if (text[endPosition] == '\r')
            {
                endPosition++;
            }

            if (endPosition >= text.Length)
            {
                return endPosition;
            }

            if (text[endPosition] == '\n')
            {
                return endPosition + 1;
            }

            return endPosition;
        }

        private static string GetUsingsText(string @namespace)
            => $"@using {@namespace}{Environment.NewLine}";

        private static string GetUsingsText(ImmutableArray<RazorDirectiveSyntax> usingDirectives, ImmutableArray<string> newUsings, ImmutableArray<string> removedUsings)
        {
            using var _ = StringBuilderPool.GetPooledObject(out var builder);

            var usingsMap = new Dictionary<string, RazorDirectiveSyntax?>(newUsings.Length + usingDirectives.Length);
            foreach (var @using in newUsings)
            {
                usingsMap.Add(@using, null);
            }

            foreach (var directive in usingDirectives)
            {
                if (RazorSyntaxFacts.TryGetNamespaceFromDirective(directive, out var @namespace))
                {
                    usingsMap[@namespace] = directive;
                }
            }

            if (usingsMap.Count == 0)
            {
                return "";
            }

            var sortedUsingsAndDirectives = usingsMap
                .OrderBy(static kvp => kvp.Key, UsingsStringComparer.Instance)
                .ToImmutableArray();

            var usingsAndDirectivesWithNewLine = sortedUsingsAndDirectives.Take(sortedUsingsAndDirectives.Length - 1);
            foreach (var (@namespace, directive) in usingsAndDirectivesWithNewLine)
            {
                AddIfNotRemoved(@namespace, directive, true);
            }

            var (lastNamespace, lastDirective) = sortedUsingsAndDirectives[^1];
            AddIfNotRemoved(lastNamespace, lastDirective, false);

            builder.AppendLine();
            return builder.ToString();

            void AddIfNotRemoved(string @namespace, RazorDirectiveSyntax? directive, bool appendNewLine)
            {
                if (directive is not null)
                {
                    if (removedUsings.Contains(@namespace))
                    {
                        return;
                    }

                    builder.Append(directive.GetContent());
                }
                else
                {
                    builder.Append($"@using {@namespace}");
                }

                if (appendNewLine)
                {
                    builder.AppendLine();
                }
            }
        }
    }
}
