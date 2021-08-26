// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions
{
    internal static class VSLSPTextSpanExtensions
    {
        public static Range AsLSPRange(this TextSpan span, SourceText sourceText)
        {
            var range = span.AsRange(sourceText);
            return new Range()
            {
                Start = new Position(range.Start.Line, range.Start.Character),
                End = new Position(range.End.Line, range.End.Character)
            };
        }
    }
}
