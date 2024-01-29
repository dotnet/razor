// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.CodeAnalysis.Razor.DynamicFiles;

[Shared]
[Export(typeof(IRazorDocumentServiceProviderFactory))]
internal sealed class RazorDocumentServiceProviderFactory : IRazorDocumentServiceProviderFactory
{
    public IRazorDocumentServiceProvider Create(IDynamicDocumentContainer documentContainer)
    {
        if (documentContainer is null)
        {
            throw new ArgumentNullException(nameof(documentContainer));
        }

        return new RazorDocumentServiceProvider(documentContainer);
    }

    public IRazorDocumentServiceProvider CreateEmpty()
    {
        return new RazorDocumentServiceProvider();
    }
}
