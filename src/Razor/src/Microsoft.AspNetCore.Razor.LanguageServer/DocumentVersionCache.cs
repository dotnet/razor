﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal abstract class DocumentVersionCache : ProjectSnapshotChangeTrigger
    {
        public abstract bool TryGetDocumentVersion(DocumentSnapshot documentSnapshot, out int? version);

        public abstract void TrackDocumentVersion(DocumentSnapshot documentSnapshot, int version);
    }
}
