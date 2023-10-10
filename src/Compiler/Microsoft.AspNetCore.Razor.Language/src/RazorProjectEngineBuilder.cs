// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class RazorProjectEngineBuilder
{
    public RazorConfiguration Configuration { get; }
    public RazorProjectFileSystem FileSystem { get; }
    public ICollection<IRazorFeature> Features { get; }
    public IList<IRazorEnginePhase> Phases { get; }

    internal RazorProjectEngineBuilder(RazorConfiguration configuration, RazorProjectFileSystem fileSystem)
    {
        if (fileSystem == null)
        {
            throw new ArgumentNullException(nameof(fileSystem));
        }

        Configuration = configuration;
        FileSystem = fileSystem;
        Features = new List<IRazorFeature>();
        Phases = new List<IRazorEnginePhase>();
    }

    public RazorProjectEngine Build()
    {
        var engineFeatures = Features.OfType<IRazorEngineFeature>().ToArray();
        var phases = Phases.ToArray();
        var engine = new RazorEngine(engineFeatures, phases);

        var projectFeatures = Features.OfType<IRazorProjectEngineFeature>().ToArray();
        var projectEngine = new RazorProjectEngine(Configuration, engine, FileSystem, projectFeatures);

        return projectEngine;
    }
}
