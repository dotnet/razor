// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost;

internal sealed class ProjectEngineFactory(string configurationName, string assemblyName) : IProjectEngineFactory
{
    private readonly string _assemblyName = assemblyName;

    public string ConfigurationName => configurationName;
    public bool SupportsSerialization => true;

    public RazorProjectEngine Create(
        RazorConfiguration configuration,
        RazorProjectFileSystem fileSystem,
        Action<RazorProjectEngineBuilder>? configure)
    {
        // Rewrite the assembly name into a full name just like this one, but with the name of the MVC design time assembly.
        var assemblyFullName = typeof(RazorProjectEngine).Assembly.FullName.AssumeNotNull();

        var assemblyName = new AssemblyName(assemblyFullName)
        {
            Name = _assemblyName
        };

        var extension = new AssemblyExtension(configuration.ConfigurationName, Assembly.Load(assemblyName));
        var initializer = extension.CreateInitializer();

        return RazorProjectEngine.Create(configuration, fileSystem, builder =>
        {
            CompilerFeatures.Register(builder);
            initializer.Initialize(builder);
            configure?.Invoke(builder);
        });
    }
}
