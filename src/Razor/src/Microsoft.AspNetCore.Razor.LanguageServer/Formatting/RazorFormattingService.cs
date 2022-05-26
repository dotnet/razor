// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using System;
using VS = Microsoft.VisualStudio.LanguageServer.Protocol;
using Omni = OmniSharp.Extensions.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal abstract class RazorFormattingService
    {
        public abstract Task<VS.TextEdit[]> FormatAsync(
            Uri uri,
            DocumentSnapshot documentSnapshot,
            VS.Range range,
            VS.FormattingOptions options,
            CancellationToken cancellationToken);

        public abstract Task<VS.TextEdit[]> FormatOnTypeAsync(
           Uri uri,
           DocumentSnapshot documentSnapshot,
           RazorLanguageKind kind,
           VS.TextEdit[] formattedEdits,
           VS.FormattingOptions options,
           int hostDocumentIndex,
           char triggerCharacter,
           CancellationToken cancellationToken);

        public abstract Task<Omni.Models.TextEdit[]> OmniFormatOnTypeAsync(
           Uri uri,
           DocumentSnapshot documentSnapshot,
           RazorLanguageKind kind,
           Omni.Models.TextEdit[] formattedEdits,
           Omni.Models.FormattingOptions options,
           int hostDocumentIndex,
           char triggerCharacter,
           CancellationToken cancellationToken);

        public abstract Task<VS.TextEdit[]> FormatCodeActionAsync(
            Uri uri,
            DocumentSnapshot documentSnapshot,
            RazorLanguageKind kind,
            VS.TextEdit[] formattedEdits,
            VS.FormattingOptions options,
            CancellationToken cancellationToken);

        public abstract Task<VS.TextEdit[]> FormatSnippetAsync(
            Uri uri,
            DocumentSnapshot documentSnapshot,
            RazorLanguageKind kind,
            VS.TextEdit[] formattedEdits,
            VS.FormattingOptions options,
            CancellationToken cancellationToken);
    }
}
