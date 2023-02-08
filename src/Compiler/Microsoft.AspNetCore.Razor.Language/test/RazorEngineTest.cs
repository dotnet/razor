﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language;
#pragma warning disable CS0618 // Type or member is obsolete
public class RazorEngineTest
{
    [Fact]
    public void Create_NoArg_CreatesDefaultRuntimeEngine()
    {
        // Arrange
        // Act
        var engine = RazorEngine.Create();

        // Assert
        Assert.IsType<DefaultRazorEngine>(engine);
        AssertDefaultRuntimeFeatures(engine.Features);
        AssertDefaultRuntimePhases(engine.Phases);
        AssertDefaultRuntimeTargetExtensions(engine);
    }

    [Fact]
    public void CreateDesignTime_NoArg_CreatesDefaultDesignTimeEngine()
    {
        // Arrange
        // Act
        var engine = RazorEngine.CreateDesignTime();

        // Assert
        Assert.IsType<DefaultRazorEngine>(engine);
        AssertDefaultDesignTimeFeatures(engine.Features);
        AssertDefaultDesignTimePhases(engine.Phases);
        AssertDefaultDesignTimeTargetExtensions(engine);
    }

    [Fact]
    public void Create_Null_CreatesDefaultRuntimeEngine()
    {
        // Arrange
        // Act
        var engine = RazorEngine.Create(configure: null);

        // Assert
        Assert.IsType<DefaultRazorEngine>(engine);
        AssertDefaultRuntimeFeatures(engine.Features);
        AssertDefaultRuntimePhases(engine.Phases);
        AssertDefaultRuntimeTargetExtensions(engine);
    }

    [Fact]
    public void CreateDesignTime_Null_CreatesDefaultDesignTimeEngine()
    {
        // Arrange
        // Act
        var engine = RazorEngine.CreateDesignTime(configure: null);

        // Assert
        Assert.IsType<DefaultRazorEngine>(engine);
        AssertDefaultDesignTimeFeatures(engine.Features);
        AssertDefaultDesignTimePhases(engine.Phases);
        AssertDefaultDesignTimeTargetExtensions(engine);
    }

    [Fact]
    public void Create_Lambda_AddsFeaturesAndPhases()
    {
        // Arrange
        IRazorEngineFeature[] features = null;
        IRazorEnginePhase[] phases = null;

        // Act
        var engine = RazorEngine.Create(builder =>
        {
            builder.Features.Clear();
            builder.Phases.Clear();

            builder.Features.Add(Mock.Of<IRazorEngineFeature>());
            builder.Features.Add(Mock.Of<IRazorEngineFeature>());

            builder.Phases.Add(Mock.Of<IRazorEnginePhase>());
            builder.Phases.Add(Mock.Of<IRazorEnginePhase>());

            features = builder.Features.ToArray();
            phases = builder.Phases.ToArray();
        });

        // Assert
        Assert.Collection(
            engine.Features,
            f => Assert.Same(features[0], f),
            f => Assert.Same(features[1], f));

        Assert.Collection(
            engine.Phases,
            p => Assert.Same(phases[0], p),
            p => Assert.Same(phases[1], p));
    }

    [Fact]
    public void CreateDesignTime_Lambda_AddsFeaturesAndPhases()
    {
        // Arrange
        IRazorEngineFeature[] features = null;
        IRazorEnginePhase[] phases = null;

        // Act
        var engine = RazorEngine.CreateDesignTime(builder =>
        {
            builder.Features.Clear();
            builder.Phases.Clear();

            builder.Features.Add(Mock.Of<IRazorEngineFeature>());
            builder.Features.Add(Mock.Of<IRazorEngineFeature>());

            builder.Phases.Add(Mock.Of<IRazorEnginePhase>());
            builder.Phases.Add(Mock.Of<IRazorEnginePhase>());

            features = builder.Features.ToArray();
            phases = builder.Phases.ToArray();
        });

        // Assert
        Assert.Collection(
            engine.Features,
            f => Assert.Same(features[0], f),
            f => Assert.Same(features[1], f));

        Assert.Collection(
            engine.Phases,
            p => Assert.Same(phases[0], p),
            p => Assert.Same(phases[1], p));
    }

    private static void AssertDefaultRuntimeTargetExtensions(RazorEngine engine)
    {
        var feature = engine.Features.OfType<IRazorTargetExtensionFeature>().FirstOrDefault();
        Assert.NotNull(feature);

        Assert.Collection(
            feature.TargetExtensions,
            extension => Assert.IsType<MetadataAttributeTargetExtension>(extension),
            extension => Assert.IsType<DefaultTagHelperTargetExtension>(extension),
            extension => Assert.IsType<PreallocatedAttributeTargetExtension>(extension));
    }

    private static void AssertDefaultRuntimeFeatures(IEnumerable<IRazorEngineFeature> features)
    {
        Assert.Collection(
            features,
            feature => Assert.IsType<DefaultRazorDirectiveFeature>(feature),
            feature => Assert.IsType<DefaultRazorTargetExtensionFeature>(feature),
            feature => Assert.IsType<DefaultMetadataIdentifierFeature>(feature),
            feature => Assert.IsType<DefaultDirectiveSyntaxTreePass>(feature),
            feature => Assert.IsType<HtmlNodeOptimizationPass>(feature),
            feature => Assert.IsType<DefaultDocumentClassifierPass>(feature),
            feature => Assert.IsType<MetadataAttributePass>(feature),
            feature => Assert.IsType<DirectiveRemovalOptimizationPass>(feature),
            feature => Assert.IsType<DefaultTagHelperOptimizationPass>(feature),
            feature => Assert.IsType<DefaultDocumentClassifierPassFeature>(feature),
            feature => Assert.IsType<DefaultRazorParserOptionsFeature>(feature),
            feature => Assert.IsType<DefaultRazorCodeGenerationOptionsFeature>(feature),
            feature => Assert.IsType<PreallocatedTagHelperAttributeOptimizationPass>(feature));
    }

    private static void AssertDefaultRuntimePhases(IReadOnlyList<IRazorEnginePhase> phases)
    {
        Assert.Collection(
            phases,
            phase => Assert.IsType<DefaultRazorParsingPhase>(phase),
            phase => Assert.IsType<DefaultRazorSyntaxTreePhase>(phase),
            phase => Assert.IsType<DefaultRazorTagHelperContextDiscoveryPhase>(phase),
            phase => Assert.IsType<DefaultRazorTagHelperRewritePhase>(phase),
            phase => Assert.IsType<DefaultRazorIntermediateNodeLoweringPhase>(phase),
            phase => Assert.IsType<DefaultRazorDocumentClassifierPhase>(phase),
            phase => Assert.IsType<DefaultRazorDirectiveClassifierPhase>(phase),
            phase => Assert.IsType<DefaultRazorOptimizationPhase>(phase),
            phase => Assert.IsType<DefaultRazorCSharpLoweringPhase>(phase));
    }

    private static void AssertDefaultDesignTimeTargetExtensions(RazorEngine engine)
    {
        var feature = engine.Features.OfType<IRazorTargetExtensionFeature>().FirstOrDefault();
        Assert.NotNull(feature);

        Assert.Collection(
            feature.TargetExtensions,
            extension => Assert.IsType<MetadataAttributeTargetExtension>(extension),
            extension => Assert.IsType<DefaultTagHelperTargetExtension>(extension),
            extension => Assert.IsType<DesignTimeDirectiveTargetExtension>(extension));
    }

    private static void AssertDefaultDesignTimeFeatures(IEnumerable<IRazorEngineFeature> features)
    {
        Assert.Collection(
            features,
            feature => Assert.IsType<DefaultRazorDirectiveFeature>(feature),
            feature => Assert.IsType<DefaultRazorTargetExtensionFeature>(feature),
            feature => Assert.IsType<DefaultMetadataIdentifierFeature>(feature),
            feature => Assert.IsType<DefaultDirectiveSyntaxTreePass>(feature),
            feature => Assert.IsType<HtmlNodeOptimizationPass>(feature),
            feature => Assert.IsType<DefaultDocumentClassifierPass>(feature),
            feature => Assert.IsType<MetadataAttributePass>(feature),
            feature => Assert.IsType<DirectiveRemovalOptimizationPass>(feature),
            feature => Assert.IsType<DefaultTagHelperOptimizationPass>(feature),
            feature => Assert.IsType<DefaultDocumentClassifierPassFeature>(feature),
            feature => Assert.IsType<DefaultRazorParserOptionsFeature>(feature),
            feature => Assert.IsType<DefaultRazorCodeGenerationOptionsFeature>(feature),
            feature => Assert.IsType<SuppressChecksumOptionsFeature>(feature),
            feature => Assert.IsType<DesignTimeDirectivePass>(feature));
    }

    private static void AssertDefaultDesignTimePhases(IReadOnlyList<IRazorEnginePhase> phases)
    {
        Assert.Collection(
            phases,
            phase => Assert.IsType<DefaultRazorParsingPhase>(phase),
            phase => Assert.IsType<DefaultRazorSyntaxTreePhase>(phase),
            phase => Assert.IsType<DefaultRazorTagHelperContextDiscoveryPhase>(phase),
            phase => Assert.IsType<DefaultRazorTagHelperRewritePhase>(phase),
            phase => Assert.IsType<DefaultRazorIntermediateNodeLoweringPhase>(phase),
            phase => Assert.IsType<DefaultRazorDocumentClassifierPhase>(phase),
            phase => Assert.IsType<DefaultRazorDirectiveClassifierPhase>(phase),
            phase => Assert.IsType<DefaultRazorOptimizationPhase>(phase),
            phase => Assert.IsType<DefaultRazorCSharpLoweringPhase>(phase));
    }
}
