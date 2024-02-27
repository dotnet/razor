// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost;

internal sealed partial class ProjectEngineFactory(string configurationName, string assemblyName) : IProjectEngineFactory
{
    private static readonly Dictionary<InitializerKey, RazorExtensionInitializer> s_initializerMap = [];

    private readonly string _assemblyName = assemblyName;

    public string ConfigurationName => configurationName;

    public RazorProjectEngine Create(
        RazorConfiguration configuration,
        RazorProjectFileSystem fileSystem,
        Action<RazorProjectEngineBuilder>? configure)
    {
        RazorExtensionInitializer? initializer;

        lock (s_initializerMap)
        {
            var key = new InitializerKey(configuration.ConfigurationName, _assemblyName);
            if (!s_initializerMap.TryGetValue(key, out initializer))
            {
                initializer = CreateInitializer(key);

                if (initializer is null)
                {
                    throw new InvalidOperationException(SR.FormatUnsupported_Razor_extension_0(key.ConfigurationName));
                }

                s_initializerMap.Add(key, initializer);
            }
        }

        return RazorProjectEngine.Create(configuration, fileSystem, builder =>
        {
            CompilerFeatures.Register(builder);
            initializer.Initialize(builder);
            configure?.Invoke(builder);
        });

        static RazorExtensionInitializer? CreateInitializer(InitializerKey key)
        {
            return key.ConfigurationName switch
            {
                "MVC-1.0" or "MVC-1.1" => new Mvc.Razor.Extensions.Version1_X.ExtensionInitializer(),
                "MVC-2.0" or "MVC-2.1" => new Mvc.Razor.Extensions.Version2_X.ExtensionInitializer(),
                "MVC-3.0" => new Mvc.Razor.Extensions.ExtensionInitializer(),
                _ => null,
            };
        }
    }
}
