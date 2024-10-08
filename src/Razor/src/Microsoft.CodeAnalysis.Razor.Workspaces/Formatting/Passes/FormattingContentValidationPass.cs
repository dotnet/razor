﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.Formatting;

internal sealed class FormattingContentValidationPass(ILoggerFactory loggerFactory) : IFormattingPass
{
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<FormattingContentValidationPass>();

    // Internal for testing.
    internal bool DebugAssertsEnabled { get; set; } = true;

    public Task<ImmutableArray<TextChange>> ExecuteAsync(FormattingContext context, ImmutableArray<TextChange> changes, CancellationToken cancellationToken)
    {
        var text = context.SourceText;
        var changedText = text.WithChanges(changes);

        if (!text.NonWhitespaceContentEquals(changedText))
        {
            // Looks like we removed some non-whitespace content as part of formatting. Oops.
            // Discard this formatting result.

            _logger.LogWarning($"{SR.Format_operation_changed_nonwhitespace}");

            foreach (var change in changes)
            {
                if (change.NewText?.Any(c => !char.IsWhiteSpace(c)) ?? false)
                {
                    _logger.LogWarning($"{SR.FormatEdit_at_adds(text.GetLinePositionSpan(change.Span), change.NewText)}");
                }
                else if (text.TryGetFirstNonWhitespaceOffset(change.Span, out _))
                {
                    _logger.LogWarning($"{SR.FormatEdit_at_deletes(text.GetLinePositionSpan(change.Span), text.ToString(change.Span))}");
                }
            }

            if (DebugAssertsEnabled)
            {
                Debug.Fail("A formatting result was rejected because it was going to change non-whitespace content in the document.");
            }

            return Task.FromResult<ImmutableArray<TextChange>>([]);
        }

        return Task.FromResult(changes);
    }
}
