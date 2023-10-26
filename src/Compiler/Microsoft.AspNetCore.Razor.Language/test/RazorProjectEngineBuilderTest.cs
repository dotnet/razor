﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Linq;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language;

public class RazorProjectEngineBuilderTest
{
    [Fact]
    public void Build_AddsFeaturesToRazorEngine()
    {
        // Arrange
        var builder = new RazorProjectEngineBuilder(RazorConfiguration.Default, Mock.Of<RazorProjectFileSystem>());
        builder.Features.Add(Mock.Of<IRazorEngineFeature>());
        builder.Features.Add(Mock.Of<IRazorEngineFeature>());
        builder.Features.Add(Mock.Of<IRazorProjectEngineFeature>());

        var features = builder.Features.ToArray();

        // Act
        var projectEngine = builder.Build();

        // Assert
        Assert.Collection(projectEngine.Engine.Features,
            feature => Assert.Same(features[0], feature),
            feature => Assert.Same(features[1], feature));
    }

    [Fact]
    public void Build_AddsPhasesToRazorEngine()
    {
        // Arrange
        var builder = new RazorProjectEngineBuilder(RazorConfiguration.Default, Mock.Of<RazorProjectFileSystem>());
        builder.Phases.Add(Mock.Of<IRazorEnginePhase>());
        builder.Phases.Add(Mock.Of<IRazorEnginePhase>());

        var phases = builder.Phases.ToArray();

        // Act
        var projectEngine = builder.Build();

        // Assert
        Assert.Collection(projectEngine.Engine.Phases,
            phase => Assert.Same(phases[0], phase),
            phase => Assert.Same(phases[1], phase));
    }

    [Fact]
    public void Build_CreatesProjectEngineWithFileSystem()
    {
        // Arrange
        var fileSystem = Mock.Of<RazorProjectFileSystem>();
        var builder = new RazorProjectEngineBuilder(RazorConfiguration.Default, fileSystem);

        // Act
        var projectEngine = builder.Build();

        // Assert
        Assert.Same(fileSystem, projectEngine.FileSystem);
    }
}
