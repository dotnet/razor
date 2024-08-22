// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Logging;

namespace Microsoft.CodeAnalysis.Razor.Formatting;

/// <summary>
/// Gets edits in Html files, and returns edits to Razor files, with nicely formatted Html
/// </summary>
internal sealed class HtmlOnTypeFormattingPass(ILoggerFactory loggerFactory) : HtmlFormattingPassBase(loggerFactory.GetOrCreateLogger<HtmlOnTypeFormattingPass>())
{
    public override Task<FormattingResult> ExecuteAsync(FormattingContext context, FormattingResult result, CancellationToken cancellationToken)
    {
        if (result.Edits.Length == 0)
        {
            // There are no HTML edits for us to apply. No op.
            return Task.FromResult(new FormattingResult([]));
        }

        return base.ExecuteAsync(context, result, cancellationToken);
    }
}
