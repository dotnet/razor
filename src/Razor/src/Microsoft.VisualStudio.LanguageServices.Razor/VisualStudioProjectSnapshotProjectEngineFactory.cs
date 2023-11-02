// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.LanguageServices.Razor;

[Export(typeof(IProjectSnapshotProjectEngineFactory))]
internal class VisualStudioProjectSnapshotProjectEngineFactory : ProjectSnapshotProjectEngineFactory
{
    [ImportingConstructor]
    public VisualStudioProjectSnapshotProjectEngineFactory(
        IFallbackProjectEngineFactory fallback,
        [ImportMany] Lazy<IProjectEngineFactory, ICustomProjectEngineFactoryMetadata>[] factories)
        : base(fallback, factories)
    {
    }
}
