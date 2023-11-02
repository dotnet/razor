// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor;

internal abstract class ProjectSnapshotProjectEngineFactory(
    IFallbackProjectEngineFactory fallback,
    Lazy<IProjectEngineFactory, ICustomProjectEngineFactoryMetadata>[] factories) : IProjectSnapshotProjectEngineFactory
{
    private readonly IFallbackProjectEngineFactory _fallback = fallback;
    private readonly Lazy<IProjectEngineFactory, ICustomProjectEngineFactoryMetadata>[] _factories = factories;

    public virtual RazorProjectEngine Create(
        RazorConfiguration configuration,
        RazorProjectFileSystem fileSystem,
        Action<RazorProjectEngineBuilder>? configure)
    {
        // When we're running in the editor, the editor provides a configure delegate that will include
        // the editor settings and tag helpers.
        //
        // This service is only used in process in Visual Studio, and any other callers should provide these
        // things also.
        configure ??= ((b) => { });

        // If there's no factory to handle the configuration then fall back to a very basic configuration.
        //
        // This will stop a crash from happening in this case (misconfigured project), but will still make
        // it obvious to the user that something is wrong.
        var factory = SelectFactory(configuration) ?? _fallback;
        return factory.Create(configuration, fileSystem, configure);
    }

    public IProjectEngineFactory? FindSerializableFactory(IProjectSnapshot project)
        => SelectFactory(project.Configuration, requireSerializable: true);

    private IProjectEngineFactory? SelectFactory(RazorConfiguration configuration, bool requireSerializable = false)
    {
        foreach (var factory in _factories)
        {
            if (configuration.ConfigurationName == factory.Metadata.ConfigurationName)
            {
                return requireSerializable && !factory.Metadata.SupportsSerialization ? null : factory.Value;
            }
        }

        return null;
    }
}
