﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

namespace Microsoft.AspNetCore.Razor.OmniSharpPlugin.StrongNamed
{
    public abstract class OmniSharpDocumentProcessedListener
    {
        public abstract void Initialize(OmniSharpProjectSnapshotManager projectManager);

        public abstract void DocumentProcessed(OmniSharpDocumentSnapshot document);
    }
}
