// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.CSharp;
using RazorExtensionsV1_X = Microsoft.AspNetCore.Mvc.Razor.Extensions.Version1_X.RazorExtensions;
using RazorExtensionsV2_X = Microsoft.AspNetCore.Mvc.Razor.Extensions.Version2_X.RazorExtensions;
using RazorExtensionsV3 = Microsoft.AspNetCore.Mvc.Razor.Extensions.RazorExtensions;

namespace Microsoft.AspNetCore.Razor.Language;

public static class RazorProjectEngineBuilderExtensions
{
    private static readonly ReadOnlyMemory<char> s_prefix = "MVC-".ToCharArray();

    public static void RegisterExtensions(this RazorProjectEngineBuilder builder)
    {
        var configurationName = builder.Configuration.ConfigurationName.AsSpanOrDefault();

        if (!configurationName.StartsWith(s_prefix.Span))
        {
            return;
        }

        configurationName = configurationName[s_prefix.Length..];

        switch (configurationName)
        {
            case ['1', '.', '0' or '1']: // 1.0 or 1.1
                RazorExtensionsV1_X.Register(builder);

                if (configurationName[^1] == '1') // 1.1.
                {
                    RazorExtensionsV1_X.RegisterViewComponentTagHelpers(builder);
                }

                break;

            case ['2', '.', '0' or '1']: // 2.0 or 2.1
                RazorExtensionsV2_X.Register(builder);
                break;

            case ['3', '.', '0']: // 3.0
                RazorExtensionsV3.Register(builder);
                break;
        }
    }

    public static RazorProjectEngineBuilder ConfigureParserOptions(this RazorProjectEngineBuilder builder, Action<RazorParserOptionsBuilder> configure)
    {
        ArgHelper.ThrowIfNull(builder);
        ArgHelper.ThrowIfNull(configure);

        builder.Features.Add(new ConfigureParserOptionsFeature(configure));

        return builder;
    }

    public static RazorProjectEngineBuilder ConfigureCodeGenerationOptions(this RazorProjectEngineBuilder builder, Action<RazorCodeGenerationOptionsBuilder> configure)
    {
        ArgHelper.ThrowIfNull(builder);
        ArgHelper.ThrowIfNull(configure);

        builder.Features.Add(new ConfigureCodeGenerationOptionsFeature(configure));

        return builder;
    }

    /// <summary>
    /// Registers a class configuration delegate that gets invoked during code generation.
    /// </summary>
    /// <param name="builder">The <see cref="RazorProjectEngineBuilder"/>.</param>
    /// <param name="configureClass"><see cref="Action"/> invoked to configure
    /// <see cref="ClassDeclarationIntermediateNode"/> during code generation.</param>
    /// <returns>The <see cref="RazorProjectEngineBuilder"/>.</returns>
    public static RazorProjectEngineBuilder ConfigureClass(
        this RazorProjectEngineBuilder builder,
        Action<RazorCodeDocument, ClassDeclarationIntermediateNode> configureClass)
    {
        ArgHelper.ThrowIfNull(builder);
        ArgHelper.ThrowIfNull(configureClass);

        var configurationFeature = GetDefaultDocumentClassifierPassFeature(builder);
        configurationFeature.ConfigureClass.Add(configureClass);
        return builder;
    }

    /// <summary>
    /// Sets the base type for generated types.
    /// </summary>
    /// <param name="builder">The <see cref="RazorProjectEngineBuilder"/>.</param>
    /// <param name="baseType">The name of the base type.</param>
    /// <returns>The <see cref="RazorProjectEngineBuilder"/>.</returns>
    public static RazorProjectEngineBuilder SetBaseType(this RazorProjectEngineBuilder builder, string baseType)
    {
        ArgHelper.ThrowIfNull(builder);

        var configurationFeature = GetDefaultDocumentClassifierPassFeature(builder);
        configurationFeature.ConfigureClass.Add((document, @class) => @class.BaseType = new BaseTypeWithModel(baseType));
        return builder;
    }

    /// <summary>
    /// Sets the namespace for generated types.
    /// </summary>
    /// <param name="builder">The <see cref="RazorProjectEngineBuilder"/>.</param>
    /// <param name="namespaceName">The name of the namespace.</param>
    /// <returns>The <see cref="RazorProjectEngineBuilder"/>.</returns>
    public static RazorProjectEngineBuilder SetNamespace(this RazorProjectEngineBuilder builder, string namespaceName)
    {
        ArgHelper.ThrowIfNull(builder);

        var configurationFeature = GetDefaultDocumentClassifierPassFeature(builder);
        configurationFeature.ConfigureNamespace.Add((document, @namespace) => @namespace.Content = namespaceName);
        return builder;
    }

    /// <summary>
    /// Sets the root namespace for the generated code.
    /// </summary>
    /// <param name="builder">The <see cref="RazorProjectEngineBuilder"/>.</param>
    /// <param name="rootNamespace">The root namespace.</param>
    /// <returns>The <see cref="RazorProjectEngineBuilder"/>.</returns>
    public static RazorProjectEngineBuilder SetRootNamespace(this RazorProjectEngineBuilder builder, string? rootNamespace)
    {
        ArgHelper.ThrowIfNull(builder);

        builder.ConfigureCodeGenerationOptions(builder =>
        {
            builder.RootNamespace = rootNamespace;
        });

        return builder;
    }

    /// <summary>
    /// Sets the SupportLocalizedComponentNames property to make localized component name diagnostics available.
    /// </summary>
    /// <param name="builder">The <see cref="RazorProjectEngineBuilder"/>.</param>
    /// <returns>The <see cref="RazorProjectEngineBuilder"/>.</returns>
    public static RazorProjectEngineBuilder SetSupportLocalizedComponentNames(this RazorProjectEngineBuilder builder)
    {
        ArgHelper.ThrowIfNull(builder);

        builder.ConfigureCodeGenerationOptions(builder =>
        {
            builder.SupportLocalizedComponentNames = true;
        });

        return builder;
    }

    /// <summary>
    /// Adds the specified <see cref="ICodeTargetExtension"/>.
    /// </summary>
    /// <param name="builder">The <see cref="RazorProjectEngineBuilder"/>.</param>
    /// <param name="extension">The <see cref="ICodeTargetExtension"/> to add.</param>
    /// <returns>The <see cref="RazorProjectEngineBuilder"/>.</returns>
    public static RazorProjectEngineBuilder AddTargetExtension(this RazorProjectEngineBuilder builder, ICodeTargetExtension extension)
    {
        ArgHelper.ThrowIfNull(builder);
        ArgHelper.ThrowIfNull(extension);

        var targetExtensionFeature = GetTargetExtensionFeature(builder);
        targetExtensionFeature.TargetExtensions.Add(extension);

        return builder;
    }

    /// <summary>
    /// Adds the specified <see cref="DirectiveDescriptor"/>.
    /// </summary>
    /// <param name="builder">The <see cref="RazorProjectEngineBuilder"/>.</param>
    /// <param name="directive">The <see cref="DirectiveDescriptor"/> to add.</param>
    /// <returns>The <see cref="RazorProjectEngineBuilder"/>.</returns>
    public static RazorProjectEngineBuilder AddDirective(this RazorProjectEngineBuilder builder, DirectiveDescriptor directive)
    {
        ArgHelper.ThrowIfNull(builder);
        ArgHelper.ThrowIfNull(directive);

        var directiveFeature = GetDirectiveFeature(builder);
        directiveFeature.Directives.Add(directive);

        return builder;
    }

    /// <summary>
    /// Adds the specified <see cref="DirectiveDescriptor"/> for the provided file kind.
    /// </summary>
    /// <param name="builder">The <see cref="RazorProjectEngineBuilder"/>.</param>
    /// <param name="directive">The <see cref="DirectiveDescriptor"/> to add.</param>
    /// <param name="fileKinds">The file kinds, for which to register the directive. See <see cref="FileKinds"/>.</param>
    /// <returns>The <see cref="RazorProjectEngineBuilder"/>.</returns>
    public static RazorProjectEngineBuilder AddDirective(this RazorProjectEngineBuilder builder, DirectiveDescriptor directive, params string[] fileKinds)
    {
        ArgHelper.ThrowIfNull(builder);
        ArgHelper.ThrowIfNull(directive);
        ArgHelper.ThrowIfNull(fileKinds);

        var directiveFeature = GetDirectiveFeature(builder);

        foreach (var fileKind in fileKinds)
        {
            if (!directiveFeature.DirectivesByFileKind.TryGetValue(fileKind, out var directives))
            {
                directives = [];
                directiveFeature.DirectivesByFileKind.Add(fileKind, directives);
            }

            directives.Add(directive);
        }

        return builder;
    }

    /// <summary>
    /// Adds the provided <see cref="RazorProjectItem" />s as imports to all project items processed
    /// by the <see cref="RazorProjectEngine"/>.
    /// </summary>
    /// <param name="builder">The <see cref="RazorProjectEngineBuilder"/>.</param>
    /// <param name="imports">The collection of imports.</param>
    /// <returns>The <see cref="RazorProjectEngineBuilder"/>.</returns>
    public static RazorProjectEngineBuilder AddDefaultImports(this RazorProjectEngineBuilder builder, params string[] imports)
    {
        ArgHelper.ThrowIfNull(builder);

        builder.Features.Add(new AdditionalImportsProjectFeature(imports));

        return builder;
    }

    /// <summary>
    /// Sets the C# language version to target when generating code.
    /// </summary>
    /// <param name="builder">The <see cref="RazorProjectEngineBuilder"/>.</param>
    /// <param name="csharpLanguageVersion">The C# <see cref="LanguageVersion"/>.</param>
    /// <returns>The <see cref="RazorProjectEngineBuilder"/>.</returns>
    public static RazorProjectEngineBuilder SetCSharpLanguageVersion(this RazorProjectEngineBuilder builder, LanguageVersion csharpLanguageVersion)
    {
        ArgHelper.ThrowIfNull(builder);

        var existingFeature = builder.Features.OfType<ConfigureParserForCSharpVersionFeature>().FirstOrDefault();
        if (existingFeature != null)
        {
            builder.Features.Remove(existingFeature);
        }

        // This will convert any "latest", "default" or "LatestMajor" LanguageVersions into their numerical equivalent.
        var effectiveCSharpLanguageVersion = LanguageVersionFacts.MapSpecifiedToEffectiveVersion(csharpLanguageVersion);
        builder.Features.Add(new ConfigureParserForCSharpVersionFeature(effectiveCSharpLanguageVersion));

        return builder;
    }

    private static DefaultRazorDirectiveFeature GetDirectiveFeature(RazorProjectEngineBuilder builder)
    {
        var directiveFeature = builder.Features.OfType<DefaultRazorDirectiveFeature>().FirstOrDefault();
        if (directiveFeature == null)
        {
            directiveFeature = new DefaultRazorDirectiveFeature();
            builder.Features.Add(directiveFeature);
        }

        return directiveFeature;
    }

    private static IRazorTargetExtensionFeature GetTargetExtensionFeature(RazorProjectEngineBuilder builder)
    {
        var targetExtensionFeature = builder.Features.OfType<IRazorTargetExtensionFeature>().FirstOrDefault();
        if (targetExtensionFeature == null)
        {
            targetExtensionFeature = new DefaultRazorTargetExtensionFeature();
            builder.Features.Add(targetExtensionFeature);
        }

        return targetExtensionFeature;
    }

    private static DefaultDocumentClassifierPassFeature GetDefaultDocumentClassifierPassFeature(RazorProjectEngineBuilder builder)
    {
        var configurationFeature = builder.Features.OfType<DefaultDocumentClassifierPassFeature>().FirstOrDefault();
        if (configurationFeature == null)
        {
            configurationFeature = new DefaultDocumentClassifierPassFeature();
            builder.Features.Add(configurationFeature);
        }

        return configurationFeature;
    }

    private sealed class ConfigureParserOptionsFeature(Action<RazorParserOptionsBuilder> configure) : RazorEngineFeatureBase, IConfigureRazorParserOptionsFeature
    {
        public int Order => 0;

        public void Configure(RazorParserOptionsBuilder builder)
        {
            configure(builder);
        }
    }

    private sealed class ConfigureCodeGenerationOptionsFeature(Action<RazorCodeGenerationOptionsBuilder> configure) : RazorEngineFeatureBase, IConfigureRazorCodeGenerationOptionsFeature
    {
        public int Order => 0;

        public void Configure(RazorCodeGenerationOptionsBuilder builder)
        {
            configure(builder);
        }
    }

    private sealed class AdditionalImportsProjectFeature(string[] imports) : RazorProjectEngineFeatureBase, IImportProjectFeature
    {
        private readonly ImmutableArray<RazorProjectItem> _imports = imports.SelectAsArray(
            static import => (RazorProjectItem)new DefaultImportProjectItem("Additional default imports", import));

        public void CollectImports(RazorProjectItem projectItem, ref PooledArrayBuilder<RazorProjectItem> imports)
        {
            imports.AddRange(_imports);
        }
    }

    private sealed class ConfigureParserForCSharpVersionFeature(LanguageVersion languageVersion) : RazorEngineFeatureBase, IConfigureRazorCodeGenerationOptionsFeature
    {
        public LanguageVersion CSharpLanguageVersion { get; } = languageVersion;

        public int Order { get; set; }

        public void Configure(RazorCodeGenerationOptionsBuilder builder)
        {
            if (builder.Configuration is { LanguageVersion.Major: < 3 })
            {
                // Prior to 3.0 there were no C# version specific controlled features. Suppress nullability enforcement.
                builder.SuppressNullabilityEnforcement = true;
            }
            else if (CSharpLanguageVersion < LanguageVersion.CSharp8)
            {
                // Having nullable flags < C# 8.0 would cause compile errors.
                builder.SuppressNullabilityEnforcement = true;
            }
            else
            {
                // Given that nullability enforcement can be a compile error we only turn it on for C# >= 8.0. There are
                // cases in tooling when the project isn't fully configured yet at which point the CSharpLanguageVersion
                // may be Default (value 0). In those cases that C# version is equivalently "unspecified" and is up to the consumer
                // to act in a safe manner to not cause unneeded errors for older compilers. Therefore if the version isn't
                // >= 8.0 (Latest has a higher value) then nullability enforcement is suppressed.
                //
                // Once the project finishes configuration the C# language version will be updated to reflect the effective
                // language version for the project by our workspace change detectors. That mechanism extracts the correlated
                // Roslyn project and acquires the effective C# version at that point.
                builder.SuppressNullabilityEnforcement = false;
            }

            if (builder.Configuration is { LanguageVersion.Major: >= 5 })
            {
                // This is a useful optimization but isn't supported by older framework versions
                builder.OmitMinimizedComponentAttributeValues = true;
            }

            if (CSharpLanguageVersion >= LanguageVersion.CSharp10)
            {
                builder.UseEnhancedLinePragma = true;
            }
        }
    }
}
