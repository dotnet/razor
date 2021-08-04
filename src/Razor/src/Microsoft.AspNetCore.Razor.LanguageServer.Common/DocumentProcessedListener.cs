// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal abstract class DocumentProcessedListener
    {
        public abstract void Initialize(ProjectSnapshotManager projectManager);

        public abstract void DocumentProcessed(DocumentSnapshot document);
    }
}
