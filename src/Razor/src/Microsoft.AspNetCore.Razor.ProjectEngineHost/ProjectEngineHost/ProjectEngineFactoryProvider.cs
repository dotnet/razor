// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost;

internal class ProjectEngineFactoryProvider(ImmutableArray<IProjectEngineFactory> factories) : IProjectEngineFactoryProvider
{
    [return: NotNullIfNotNull(nameof(fallbackFactory))]
    public IProjectEngineFactory? GetFactory(
        RazorConfiguration configuration,
        IProjectEngineFactory? fallbackFactory = null,
        bool requireSerializationSupport = false)
    {
        foreach (var factory in factories)
        {
            if (factory.ConfigurationName == configuration.ConfigurationName &&
                (!requireSerializationSupport || factory.SupportsSerialization))
            {
                return factory;
            }
        }

        return fallbackFactory;
    }
}
