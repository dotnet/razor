// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Formatting;

internal sealed class HtmlOnTypeFormattingPass(ILoggerFactory loggerFactory) : HtmlFormattingPassBase(loggerFactory.GetOrCreateLogger<HtmlOnTypeFormattingPass>())
{
    public override Task<FormattingResult> ExecuteAsync(FormattingContext context, FormattingResult result, CancellationToken cancellationToken)
    {
        Debug.Assert(result.Kind == RazorLanguageKind.Html);

        if (result.Edits.Length == 0)
        {
            // There are no HTML edits for us to apply. No op.
            return Task.FromResult(new FormattingResult([]));
        }

        return base.ExecuteAsync(context, result, cancellationToken);
    }
}
