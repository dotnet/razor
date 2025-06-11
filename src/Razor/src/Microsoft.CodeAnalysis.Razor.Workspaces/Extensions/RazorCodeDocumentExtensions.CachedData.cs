// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor.Threading;

namespace Microsoft.AspNetCore.Razor.Language;

internal static partial class RazorCodeDocumentExtensions
{
    private static readonly ConditionalWeakTable<RazorCodeDocument, CachedData> s_codeDocumentCache = new();

    private static CachedData GetCachedData(RazorCodeDocument codeDocument)
        => s_codeDocumentCache.GetValue(codeDocument, static doc => new CachedData(doc));

    private sealed class CachedData(RazorCodeDocument codeDocument)
    {
        private readonly RazorCodeDocument _codeDocument = codeDocument;

        private readonly SemaphoreSlim _stateLock = new(initialCount: 1);
        private SyntaxTree? _syntaxTree;
        private ImmutableArray<ClassifiedSpanInternal>? _classifiedSpans;
        private ImmutableArray<TagHelperSpanInternal>? _tagHelperSpans;

        public SyntaxTree GetOrParseSyntaxTree(CancellationToken cancellationToken)
        {
            if (_syntaxTree is { } syntaxTree)
            {
                return syntaxTree;
            }

            using (_stateLock.DisposableWait(cancellationToken))
            {
                return _syntaxTree ??= ParseSyntaxTree(_codeDocument, cancellationToken);
            }

            static SyntaxTree ParseSyntaxTree(RazorCodeDocument codeDocument, CancellationToken cancellationToken)
            {
                var csharpText = codeDocument.GetCSharpSourceText();
                return CSharpSyntaxTree.ParseText(csharpText, cancellationToken: cancellationToken);
            }
        }

        public ImmutableArray<ClassifiedSpanInternal> GetOrComputeClassifiedSpans(CancellationToken cancellationToken)
        {
            if (_classifiedSpans is { } classifiedSpans)
            {
                return classifiedSpans;
            }

            using (_stateLock.DisposableWait(cancellationToken))
            {
                return _classifiedSpans ??= _codeDocument.GetRequiredSyntaxTree().GetClassifiedSpans();
            }
        }

        public ImmutableArray<TagHelperSpanInternal> GetOrComputeTagHelperSpans(CancellationToken cancellationToken)
        {
            if (_tagHelperSpans is { } tagHelperSpans)
            {
                return tagHelperSpans;
            }

            using (_stateLock.DisposableWait(cancellationToken))
            {
                return _tagHelperSpans ??= _codeDocument.GetRequiredSyntaxTree().GetTagHelperSpans();
            }
        }
    }
}
