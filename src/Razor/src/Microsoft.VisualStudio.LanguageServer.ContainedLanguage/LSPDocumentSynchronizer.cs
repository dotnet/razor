// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage
{
    internal abstract class LSPDocumentSynchronizer : LSPDocumentChangeListener
    {
        public abstract Task<bool> TrySynchronizeVirtualDocumentAsync(int requiredHostDocumentVersion, VirtualDocumentSnapshot virtualDocument, CancellationToken cancellationToken);
    }
}
