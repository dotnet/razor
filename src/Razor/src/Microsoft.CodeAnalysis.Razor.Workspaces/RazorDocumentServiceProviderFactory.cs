﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

internal abstract class RazorDocumentServiceProviderFactory
{
    public abstract IRazorDocumentServiceProvider CreateEmpty();

    public abstract IRazorDocumentServiceProvider Create(DynamicDocumentContainer documentContainer);
}
