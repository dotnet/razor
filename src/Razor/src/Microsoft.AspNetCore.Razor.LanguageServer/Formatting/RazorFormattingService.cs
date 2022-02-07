// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal abstract class RazorFormattingService
    {
        public abstract Task<TextEdit[]> FormatAsync(
            DocumentUri uri,
            DocumentSnapshot documentSnapshot,
            Range range,
            FormattingOptions options,
            CancellationToken cancellationToken);

        public abstract Task<TextEdit[]> FormatOnTypeAsync(
           DocumentUri uri,
           DocumentSnapshot documentSnapshot,
           RazorLanguageKind kind,
           TextEdit[] formattedEdits,
           FormattingOptions options,
           int hostDocumentIndex,
           char triggerCharacter,
           CancellationToken cancellationToken);

        public abstract Task<TextEdit[]> FormatCodeActionAsync(
            DocumentUri uri,
            DocumentSnapshot documentSnapshot,
            RazorLanguageKind kind,
            TextEdit[] formattedEdits,
            FormattingOptions options,
            CancellationToken cancellationToken);

        public abstract Task<TextEdit[]> FormatSnippetAsync(
            DocumentUri uri,
            DocumentSnapshot documentSnapshot,
            RazorLanguageKind kind,
            TextEdit[] formattedEdits,
            FormattingOptions options,
            CancellationToken cancellationToken);
    }
}
