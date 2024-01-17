// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost;

internal class DefaultProjectEngineFactory : IProjectEngineFactory
{
    public string ConfigurationName => "Default";
    public bool SupportsSerialization => true;

    internal static RazorProjectEngine Create(
        RazorConfiguration configuration,
        RazorProjectFileSystem fileSystem,
        Action<RazorProjectEngineBuilder>? configure,
        IProjectEngineFactory fallback,
        ImmutableArray<IProjectEngineFactory> factories)
    {
        var factoryToUse = fallback;
        foreach (var factory in factories)
        {
            if (configuration.ConfigurationName == factory.ConfigurationName)
            {
                factoryToUse = factory;
            }
        }

        return factoryToUse.Create(configuration, fileSystem, configure);
    }

    public RazorProjectEngine Create(RazorConfiguration configuration, RazorProjectFileSystem fileSystem, Action<RazorProjectEngineBuilder>? configure)
    {
        return RazorProjectEngine.Create(configuration, fileSystem, b =>
        {
            CompilerFeatures.Register(b);

            configure?.Invoke(b);
        });
    }
}
