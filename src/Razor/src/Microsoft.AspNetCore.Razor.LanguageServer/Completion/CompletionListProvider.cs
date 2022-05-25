// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal abstract class CompletionListProvider
    {
        public abstract ImmutableHashSet<string> TriggerCharacters { get; }

        public abstract Task<VSInternalCompletionList?> GetCompletionListAsync(
            int absoluteIndex,
            VSInternalCompletionContext completionContext,
            DocumentContext documentContext,
            VSInternalClientCapabilities clientCapabilities,
            CancellationToken cancellationToken);
    }
}
