// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage
{
    internal abstract class LSPDocumentSynchronizer : LSPDocumentChangeListener
    {
        public abstract Task<bool> TrySynchronizeVirtualDocumentAsync(int requiredHostDocumentVersion, VirtualDocumentSnapshot virtualDocument, CancellationToken cancellationToken);

        /// <summary>
        /// Attempts to synchronize a virtual document to a corresponding host document version.
        /// </summary>
        /// <param name="requiredHostDocumentVersion">The corresponding host document version required for the generated <paramref name="virtualDocument"/></param>
        /// <param name="virtualDocument">A generated document to correlate <paramref name="requiredHostDocumentVersion"/>'s for.</param>
        /// <param name="rejectOnNewerParallelRequest">
        /// When <c>true</c> if a second synchronization request for the same virtual document comes in with a newer required host document version all pending synchronization requests will be rejected.
        /// If <c>false</c>, active synchronization requests will be fulfilled as virtual document buffers get updated. Fulfillment could be a rejection or an approval.
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <returns><c>true</c> if we were able to successfully synchronize; <c>false</c> otherwise.</returns>
        public virtual Task<bool> TrySynchronizeVirtualDocumentAsync(int requiredHostDocumentVersion, VirtualDocumentSnapshot virtualDocument, bool rejectOnNewerParallelRequest, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
