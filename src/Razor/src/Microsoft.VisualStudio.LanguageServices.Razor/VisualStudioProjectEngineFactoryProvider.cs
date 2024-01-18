// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectEngineHost;

namespace Microsoft.VisualStudio.LanguageServices.Razor;

[Export(typeof(IProjectEngineFactoryProvider))]
internal sealed class VisualStudioProjectEngineFactoryProvider : IProjectEngineFactoryProvider
{
    [return: NotNullIfNotNull(nameof(fallbackFactory))]
    public IProjectEngineFactory? GetFactory(
        RazorConfiguration configuration,
        IProjectEngineFactory? fallbackFactory = null,
        bool requireSerializationSupport = false)
        => ProjectEngineFactories.DefaultProvider.GetFactory(configuration, fallbackFactory, requireSerializationSupport);
}
