// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Folding
{
    internal abstract class RazorFoldingRangeProvider
    {
        public abstract Task<ImmutableArray<FoldingRange>> GetFoldingRangesAsync(RazorCodeDocument codeDocument, DocumentSnapshot documentSnapshot, CancellationToken cancellationToken);
    }
}
