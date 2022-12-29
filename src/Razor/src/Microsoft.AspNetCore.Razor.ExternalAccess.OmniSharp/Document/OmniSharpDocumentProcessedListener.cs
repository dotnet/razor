// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft;
using Microsoft.AspNetCore.Razor.ExternalAccess.OmniSharp.Project;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.OmniSharp.Document;

public abstract class OmniSharpDocumentProcessedListener
{
    internal abstract void Initialize(OmniSharpProjectSnapshotManager projectManager);

    internal abstract void DocumentProcessed(RazorCodeDocument codeDocument, OmniSharpDocumentSnapshot document);
}
