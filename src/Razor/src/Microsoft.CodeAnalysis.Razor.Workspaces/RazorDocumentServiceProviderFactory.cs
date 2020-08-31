// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.Workspaces
{
    internal abstract class RazorDocumentServiceProviderFactory : ProjectSnapshotChangeTrigger
    {
        public abstract IRazorDocumentServiceProvider CreateEmpty();

        public abstract IRazorDocumentServiceProvider Create(DynamicDocumentContainer documentContainer);
    }
}
