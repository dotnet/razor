﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language;

public class RazorProjectEngine
{
    public RazorConfiguration Configuration { get; }
    public RazorProjectFileSystem FileSystem { get; }
    public RazorEngine Engine { get; }
    public ImmutableArray<IRazorEnginePhase> Phases => Engine.Phases;
    public ImmutableArray<IRazorProjectEngineFeature> Features { get; }

    private readonly FeatureCache<IRazorProjectEngineFeature> _featureCache;

    internal RazorProjectEngine(
        RazorConfiguration configuration,
        RazorEngine engine,
        RazorProjectFileSystem fileSystem,
        ImmutableArray<IRazorProjectEngineFeature> features)
    {
        Configuration = configuration;
        Engine = engine;
        FileSystem = fileSystem;
        Features = features;

        _featureCache = new(features);

        foreach (var projectFeature in features)
        {
            projectFeature.Initialize(this);
        }
    }

    public ImmutableArray<TFeature> GetFeatures<TFeature>()
        where TFeature : class, IRazorProjectEngineFeature
        => _featureCache.GetFeatures<TFeature>();

    public RazorCodeDocument Process(RazorProjectItem projectItem, CancellationToken cancellationToken = default)
    {
        ArgHelper.ThrowIfNull(projectItem);

        var codeDocument = CreateCodeDocumentCore(projectItem);
        ProcessCore(codeDocument, cancellationToken);
        return codeDocument;
    }

    public RazorCodeDocument Process(
        RazorSourceDocument source,
        string fileKind,
        ImmutableArray<RazorSourceDocument> importSources,
        IReadOnlyList<TagHelperDescriptor>? tagHelpers,
        CancellationToken cancellationToken = default)
    {
        ArgHelper.ThrowIfNull(source);
        ArgHelper.ThrowIfNull(fileKind);

        var codeDocument = CreateCodeDocumentCore(source, fileKind, importSources, tagHelpers, cssScope: null, configureParser: null, configureCodeGeneration: null);
        ProcessCore(codeDocument, cancellationToken);
        return codeDocument;
    }

    public RazorCodeDocument ProcessDeclarationOnly(RazorProjectItem projectItem, CancellationToken cancellationToken = default)
    {
        ArgHelper.ThrowIfNull(projectItem);

        var codeDocument = CreateCodeDocumentCore(projectItem, configureParser: null, configureCodeGeneration: (builder) =>
        {
            builder.SuppressPrimaryMethodBody = true;
        });

        ProcessCore(codeDocument, cancellationToken);
        return codeDocument;
    }

    public RazorCodeDocument ProcessDeclarationOnly(
        RazorSourceDocument source,
        string fileKind,
        ImmutableArray<RazorSourceDocument> importSources,
        IReadOnlyList<TagHelperDescriptor>? tagHelpers,
        CancellationToken cancellationToken = default)
    {
        ArgHelper.ThrowIfNull(source);
        ArgHelper.ThrowIfNull(fileKind);

        var codeDocument = CreateCodeDocumentCore(source, fileKind, importSources, tagHelpers, cssScope: null, configureParser: null, configureCodeGeneration: (builder) =>
        {
            builder.SuppressPrimaryMethodBody = true;
        });

        ProcessCore(codeDocument, cancellationToken);
        return codeDocument;
    }

    public RazorCodeDocument ProcessDesignTime(RazorProjectItem projectItem, CancellationToken cancellationToken = default)
    {
        ArgHelper.ThrowIfNull(projectItem);

        var codeDocument = CreateCodeDocumentDesignTimeCore(projectItem);
        ProcessCore(codeDocument, cancellationToken);
        return codeDocument;
    }

    public RazorCodeDocument ProcessDesignTime(
        RazorSourceDocument source,
        string fileKind,
        ImmutableArray<RazorSourceDocument> importSources,
        IReadOnlyList<TagHelperDescriptor>? tagHelpers,
        CancellationToken cancellationToken = default)
    {
        ArgHelper.ThrowIfNull(source);
        ArgHelper.ThrowIfNull(fileKind);

        var codeDocument = CreateCodeDocumentDesignTimeCore(source, fileKind, importSources, tagHelpers, configureParser: null, configureCodeGeneration: null);
        ProcessCore(codeDocument, cancellationToken);
        return codeDocument;
    }

    internal RazorCodeDocument CreateCodeDocument(RazorProjectItem projectItem, bool designTime)
    {
        ArgHelper.ThrowIfNull(projectItem);

        return designTime
            ? CreateCodeDocumentDesignTimeCore(projectItem)
            : CreateCodeDocumentCore(projectItem);
    }

    internal RazorCodeDocument CreateCodeDocument(RazorSourceDocument source, string fileKind)
    {
        ArgHelper.ThrowIfNull(source);

        return CreateCodeDocumentCore(source, fileKind, importSources: default, tagHelpers: null, cssScope: null, configureParser: null, configureCodeGeneration: null);
    }

    private RazorCodeDocument CreateCodeDocumentCore(
        RazorProjectItem projectItem,
        Action<RazorParserOptionsBuilder>? configureParser = null,
        Action<RazorCodeGenerationOptionsBuilder>? configureCodeGeneration = null)
    {
        var source = projectItem.GetSource();
        var importSources = GetImportSources(projectItem, designTime: false);

        return CreateCodeDocumentCore(
            source, projectItem.FileKind, importSources, tagHelpers: null, cssScope: projectItem.CssScope, configureParser, configureCodeGeneration);
    }

    private RazorCodeDocument CreateCodeDocumentCore(
        RazorSourceDocument source,
        string fileKind,
        ImmutableArray<RazorSourceDocument> importSources,
        IReadOnlyList<TagHelperDescriptor>? tagHelpers,
        string? cssScope,
        Action<RazorParserOptionsBuilder>? configureParser,
        Action<RazorCodeGenerationOptionsBuilder>? configureCodeGeneration)
    {
        var parserOptions = GetRequiredFeature<IRazorParserOptionsFactoryProjectFeature>().Create(fileKind, builder =>
        {
            ConfigureParserOptions(builder);
            configureParser?.Invoke(builder);
        });

        var codeGenerationOptions = GetRequiredFeature<IRazorCodeGenerationOptionsFactoryProjectFeature>().Create(builder =>
        {
            ConfigureCodeGenerationOptions(builder);
            configureCodeGeneration?.Invoke(builder);
        });

        var codeDocument = RazorCodeDocument.Create(source, importSources, parserOptions, codeGenerationOptions);

        codeDocument.SetTagHelpers(tagHelpers);
        codeDocument.SetFileKind(fileKind);

        if (cssScope != null)
        {
            codeDocument.SetCssScope(cssScope);
        }

        return codeDocument;
    }

    private RazorCodeDocument CreateCodeDocumentDesignTimeCore(
        RazorProjectItem projectItem,
        Action<RazorParserOptionsBuilder>? configureParser = null,
        Action<RazorCodeGenerationOptionsBuilder>? configureCodeGeneration = null)
    {
        var source = projectItem.GetSource();
        var importSources = GetImportSources(projectItem, designTime: true);

        return CreateCodeDocumentDesignTimeCore(source, projectItem.FileKind, importSources, tagHelpers: null, configureParser, configureCodeGeneration);
    }

    private RazorCodeDocument CreateCodeDocumentDesignTimeCore(
        RazorSourceDocument sourceDocument,
        string fileKind,
        ImmutableArray<RazorSourceDocument> importSources,
        IReadOnlyList<TagHelperDescriptor>? tagHelpers,
        Action<RazorParserOptionsBuilder>? configureParser,
        Action<RazorCodeGenerationOptionsBuilder>? configureCodeGeneration)
    {
        ArgHelper.ThrowIfNull(sourceDocument);

        var parserOptions = GetRequiredFeature<IRazorParserOptionsFactoryProjectFeature>().Create(fileKind, builder =>
        {
            ConfigureDesignTimeParserOptions(builder);
            configureParser?.Invoke(builder);
        });

        var codeGenerationOptions = GetRequiredFeature<IRazorCodeGenerationOptionsFactoryProjectFeature>().Create(builder =>
        {
            ConfigureDesignTimeCodeGenerationOptions(builder);
            configureCodeGeneration?.Invoke(builder);
        });

        var codeDocument = RazorCodeDocument.Create(sourceDocument, importSources, parserOptions, codeGenerationOptions);

        codeDocument.SetTagHelpers(tagHelpers);
        codeDocument.SetFileKind(fileKind);

        return codeDocument;
    }

    private void ProcessCore(RazorCodeDocument codeDocument, CancellationToken cancellationToken)
    {
        ArgHelper.ThrowIfNull(codeDocument);

        Engine.Process(codeDocument, cancellationToken);
    }

    private TFeature GetRequiredFeature<TFeature>()
        where TFeature : class, IRazorProjectEngineFeature
    {
        if (GetFeatures<TFeature>() is [var feature, ..])
        {
            return feature;
        }

        throw new InvalidOperationException(
            Resources.FormatRazorProjectEngineMissingFeatureDependency(
                typeof(RazorProjectEngine).FullName,
                typeof(TFeature).FullName));
    }

    internal static RazorProjectEngine CreateEmpty(Action<RazorProjectEngineBuilder>? configure = null)
    {
        var builder = new RazorProjectEngineBuilder(RazorConfiguration.Default, RazorProjectFileSystem.Empty);

        configure?.Invoke(builder);

        return builder.Build();
    }

    internal static RazorProjectEngine Create(Action<RazorProjectEngineBuilder> configure)
        => Create(RazorConfiguration.Default, RazorProjectFileSystem.Empty, configure);

    public static RazorProjectEngine Create(RazorConfiguration configuration, RazorProjectFileSystem fileSystem)
        => Create(configuration, fileSystem, configure: null);

    public static RazorProjectEngine Create(
        RazorConfiguration configuration,
        RazorProjectFileSystem fileSystem,
        Action<RazorProjectEngineBuilder>? configure)
    {
        ArgHelper.ThrowIfNull(configuration);
        ArgHelper.ThrowIfNull(fileSystem);

        var builder = new RazorProjectEngineBuilder(configuration, fileSystem);

        // The initialization order is somewhat important.
        //
        // Defaults -> Extensions -> Additional customization
        //
        // This allows extensions to rely on default features, and customizations to override choices made by
        // extensions.
        AddDefaultPhases(builder.Phases);
        AddDefaultFeatures(builder.Features);

        if (configuration.LanguageVersion >= RazorLanguageVersion.Version_5_0)
        {
            builder.Features.Add(new ViewCssScopePass());
        }

        if (configuration.LanguageVersion >= RazorLanguageVersion.Version_3_0)
        {
            FunctionsDirective.Register(builder);
            ImplementsDirective.Register(builder);
            InheritsDirective.Register(builder);
            NamespaceDirective.Register(builder);
            AttributeDirective.Register(builder);

            AddComponentFeatures(builder, configuration.LanguageVersion);
        }

        configure?.Invoke(builder);

        return builder.Build();
    }

    private static void AddDefaultPhases(ImmutableArray<IRazorEnginePhase>.Builder phases)
    {
        phases.Add(new DefaultRazorParsingPhase());
        phases.Add(new DefaultRazorSyntaxTreePhase());
        phases.Add(new DefaultRazorTagHelperContextDiscoveryPhase());
        phases.Add(new DefaultRazorTagHelperRewritePhase());
        phases.Add(new DefaultRazorIntermediateNodeLoweringPhase());
        phases.Add(new DefaultRazorDocumentClassifierPhase());
        phases.Add(new DefaultRazorDirectiveClassifierPhase());
        phases.Add(new DefaultRazorOptimizationPhase());
        phases.Add(new DefaultRazorCSharpLoweringPhase());
    }

    private static void AddDefaultFeatures(ImmutableArray<IRazorFeature>.Builder features)
    {
        features.Add(new DefaultImportProjectFeature());

        // General extensibility
        features.Add(new DefaultRazorDirectiveFeature());
        features.Add(new DefaultMetadataIdentifierFeature());

        // Options features
        features.Add(new DefaultRazorParserOptionsFactoryProjectFeature());
        features.Add(new DefaultRazorCodeGenerationOptionsFactoryProjectFeature());

        // Legacy options features
        //
        // These features are obsolete as of 2.1. Our code will resolve this but not invoke them.
        features.Add(new DefaultRazorParserOptionsFeature(designTime: false, version: RazorLanguageVersion.Version_2_0, fileKind: null));
        features.Add(new DefaultRazorCodeGenerationOptionsFeature(designTime: false));

        // Syntax Tree passes
        features.Add(new DefaultDirectiveSyntaxTreePass());
        features.Add(new HtmlNodeOptimizationPass());

        // Intermediate Node Passes
        features.Add(new DefaultDocumentClassifierPass());
        features.Add(new MetadataAttributePass());
        features.Add(new DesignTimeDirectivePass());
        features.Add(new DirectiveRemovalOptimizationPass());
        features.Add(new DefaultTagHelperOptimizationPass());
        features.Add(new PreallocatedTagHelperAttributeOptimizationPass());
        features.Add(new EliminateMethodBodyPass());

        // Default Code Target Extensions
        var targetExtensionFeature = new DefaultRazorTargetExtensionFeature();
        features.Add(targetExtensionFeature);
        targetExtensionFeature.TargetExtensions.Add(new MetadataAttributeTargetExtension());
        targetExtensionFeature.TargetExtensions.Add(new DefaultTagHelperTargetExtension());
        targetExtensionFeature.TargetExtensions.Add(new PreallocatedAttributeTargetExtension());
        targetExtensionFeature.TargetExtensions.Add(new DesignTimeDirectiveTargetExtension());

        // Default configuration
        var configurationFeature = new DefaultDocumentClassifierPassFeature();
        features.Add(configurationFeature);
        configurationFeature.ConfigureClass.Add((document, @class) =>
        {
            @class.ClassName = "Template";
            @class.Modifiers.Add("public");
        });

        configurationFeature.ConfigureNamespace.Add((document, @namespace) =>
        {
            @namespace.Content = "Razor";
        });

        configurationFeature.ConfigureMethod.Add((document, method) =>
        {
            method.MethodName = "ExecuteAsync";
            method.ReturnType = $"global::{typeof(Task).FullName}";

            method.Modifiers.Add("public");
            method.Modifiers.Add("async");
            method.Modifiers.Add("override");
        });
    }

    private static void AddComponentFeatures(RazorProjectEngineBuilder builder, RazorLanguageVersion razorLanguageVersion)
    {
        // Project Engine Features
        builder.Features.Add(new ComponentImportProjectFeature());

        // Directives (conditional on file kind)
        ComponentCodeDirective.Register(builder);
        ComponentInjectDirective.Register(builder);
        ComponentLayoutDirective.Register(builder);
        ComponentPageDirective.Register(builder);

        if (razorLanguageVersion >= RazorLanguageVersion.Version_6_0)
        {
            ComponentConstrainedTypeParamDirective.Register(builder);
        }
        else
        {
            ComponentTypeParamDirective.Register(builder);
        }

        if (razorLanguageVersion >= RazorLanguageVersion.Version_5_0)
        {
            ComponentPreserveWhitespaceDirective.Register(builder);
        }

        if (razorLanguageVersion >= RazorLanguageVersion.Version_8_0)
        {
            ComponentRenderModeDirective.Register(builder);
        }

        // Document Classifier
        builder.Features.Add(new ComponentDocumentClassifierPass(razorLanguageVersion));

        // Directive Classifier
        builder.Features.Add(new ComponentWhitespacePass());

        // Optimization
        builder.Features.Add(new ComponentComplexAttributeContentPass());
        builder.Features.Add(new ComponentLoweringPass());
        builder.Features.Add(new ComponentEventHandlerLoweringPass());
        builder.Features.Add(new ComponentKeyLoweringPass());
        builder.Features.Add(new ComponentReferenceCaptureLoweringPass());
        builder.Features.Add(new ComponentSplatLoweringPass());
        builder.Features.Add(new ComponentFormNameLoweringPass());
        builder.Features.Add(new ComponentBindLoweringPass(razorLanguageVersion >= RazorLanguageVersion.Version_7_0));
        builder.Features.Add(new ComponentRenderModeLoweringPass());
        builder.Features.Add(new ComponentCssScopePass());
        builder.Features.Add(new ComponentTemplateDiagnosticPass());
        builder.Features.Add(new ComponentGenericTypePass());
        builder.Features.Add(new ComponentChildContentDiagnosticPass());
        builder.Features.Add(new ComponentMarkupDiagnosticPass());
        builder.Features.Add(new ComponentMarkupBlockPass(razorLanguageVersion));
        builder.Features.Add(new ComponentMarkupEncodingPass(razorLanguageVersion));
    }

    private ImmutableArray<RazorSourceDocument> GetImportSources(RazorProjectItem projectItem, bool designTime)
    {
        using var importItems = new PooledArrayBuilder<RazorProjectItem>();

        foreach (var importProjectFeature in GetFeatures<IImportProjectFeature>())
        {
            importItems.AddRange(importProjectFeature.GetImports(projectItem));
        }

        // Suppress exceptions for design-time requests.
        return GetImportSourceDocuments(in importItems, suppressExceptions: designTime);
    }

    // Internal for testing
    internal static ImmutableArray<RazorSourceDocument> GetImportSourceDocuments(
        ref readonly PooledArrayBuilder<RazorProjectItem> importItems,
        bool suppressExceptions = false)
    {
        using var imports = new PooledArrayBuilder<RazorSourceDocument>(importItems.Count);

        foreach (var importItem in importItems)
        {
            if (importItem.Exists)
            {
                try
                {
                    // Normal import, has file paths, content etc.
                    var sourceDocument = importItem.GetSource();
                    imports.Add(sourceDocument);
                }
                catch (IOException) when (suppressExceptions)
                {
                    // Something happened when trying to read the item from disk.
                    // Catch the exception so we don't crash the editor.
                }
            }
        }

        return imports.DrainToImmutable();
    }

    private static void ConfigureParserOptions(RazorParserOptionsBuilder builder)
    {
    }

    private static void ConfigureDesignTimeParserOptions(RazorParserOptionsBuilder builder)
    {
        builder.SetDesignTime(true);
    }

    private static void ConfigureCodeGenerationOptions(RazorCodeGenerationOptionsBuilder builder)
    {
    }

    private static void ConfigureDesignTimeCodeGenerationOptions(RazorCodeGenerationOptionsBuilder builder)
    {
        builder.SetDesignTime(true);
        builder.SuppressChecksum = true;
        builder.SuppressMetadataAttributes = true;
    }
}
