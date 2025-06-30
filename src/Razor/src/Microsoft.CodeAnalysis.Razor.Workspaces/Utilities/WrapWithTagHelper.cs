// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Text;
using Roslyn.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Workspaces.Utilities;

internal static class WrapWithTagHelper
{
    public static async Task<bool> IsValidWrappingRangeAsync(RazorCodeDocument codeDocument, LinePositionSpan range, CancellationToken cancellationToken)
    {
        var result = await ValidateAndAdjustRangeInternalAsync(codeDocument, range, cancellationToken).ConfigureAwait(false);
        return result.IsValid;
    }

    public static async Task<(bool IsValid, LspRange? AdjustedRange)> ValidateAndAdjustRangeAsync(RazorCodeDocument codeDocument, LspRange range, CancellationToken cancellationToken)
    {
        var sourceText = codeDocument.Source.Text;

        if (range?.Start is not { } start ||
            !sourceText.TryGetAbsoluteIndex(start, out var hostDocumentIndex))
        {
            return (false, null);
        }

        // First thing we do is make sure we start at a non-whitespace character. This is important because in some
        // situations the whitespace can be technically C#, but move one character to the right and it's HTML. eg
        //
        // @if (true) {
        //   |   <p></p>
        // }
        //
        // Limiting this to only whitespace on the same line, as it's not clear what user expectation would be otherwise.
        var requestSpan = sourceText.GetTextSpan(range);
        var adjustedRange = range;
        if (sourceText.TryGetFirstNonWhitespaceOffset(requestSpan, out var offset, out var newLineCount) &&
            newLineCount == 0)
        {
            adjustedRange = new LspRange
            {
                Start = new Position
                {
                    Line = range.Start.Line,
                    Character = range.Start.Character + offset
                },
                End = range.End
            };
            requestSpan = sourceText.GetTextSpan(adjustedRange);
            hostDocumentIndex += offset;
        }

        // Since we're at the start of the selection, lets prefer the language to the right of the cursor if possible.
        // That way with the following situation:
        //
        // @if (true) {
        //   |<p></p>
        // }
        //
        // Instead of C#, which certainly would be expected to go in an if statement, we'll see HTML, which obviously
        // is the better choice for this operation.
        var languageKind = codeDocument.GetLanguageKind(hostDocumentIndex, rightAssociative: true);

        // However, reverse scenario is possible as well, when we have
        // <div>
        // |@if (true) {}
        // <p></p>
        // </div>
        // in which case right-associative GetLanguageKind will return Razor and left-associative will return HTML
        // We should hand that case as well, see https://github.com/dotnet/razor/issues/10819
        if (languageKind is RazorLanguageKind.Razor)
        {
            languageKind = codeDocument.GetLanguageKind(hostDocumentIndex, rightAssociative: false);
        }

        if (languageKind is not RazorLanguageKind.Html)
        {
            // In general, we don't support C# for obvious reasons, but we can support implicit expressions. ie
            //
            // <p>@curr$$entCount</p>
            //
            // We can expand the range to encompass the whole implicit expression, and then it will wrap as expected.
            // Similarly if they have selected the implicit expression, then we can continue. ie
            //
            // <p>[|@currentCount|]</p>

            var tree = codeDocument.GetSyntaxTree();
            var node = tree.Root.FindNode(requestSpan, includeWhitespace: false, getInnermostNodeForTie: true);
            if (node?.FirstAncestorOrSelf<CSharpImplicitExpressionSyntax>() is { Parent: CSharpCodeBlockSyntax codeBlock } &&
                (requestSpan == codeBlock.Span || requestSpan.Length == 0))
            {
                // Pretend we're in Html so the rest of the logic can continue
                adjustedRange = sourceText.GetRange(codeBlock.Span);
                languageKind = RazorLanguageKind.Html;
            }
        }

        if (languageKind is not RazorLanguageKind.Html)
        {
            return (false, null);
        }

        return (true, adjustedRange);
    }

    private static async Task<(bool IsValid, LinePositionSpan? AdjustedRange)> ValidateAndAdjustRangeInternalAsync(RazorCodeDocument codeDocument, LinePositionSpan range, CancellationToken cancellationToken)
    {
        var lspRange = range.ToRange();
        var result = await ValidateAndAdjustRangeAsync(codeDocument, lspRange, cancellationToken).ConfigureAwait(false);
        return (result.IsValid, result.AdjustedRange?.ToLinePositionSpan());
    }
}