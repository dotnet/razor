// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class RazorCodeGenerationOptions(
    bool indentWithTabs,
    int indentSize,
    bool designTime,
    string? rootNamespace,
    bool suppressChecksum,
    bool suppressMetadataAttributes,
    bool suppressMetadataSourceChecksumAttributes,
    bool suppressPrimaryMethodBody,
    bool suppressNullabilityEnforcement,
    bool omitMinimizedComponentAttributeValues,
    bool supportLocalizedComponentNames,
    bool useEnhancedLinePragma,
    string? suppressUniqueIds,
    bool suppressAddComponentParameter,
    bool remapLinePragmaPathsOnWindows)
{
    public bool DesignTime { get; } = designTime;
    public bool IndentWithTabs { get; } = indentWithTabs;
    public int IndentSize { get; } = indentSize;

    /// <summary>
    /// Gets the root namespace for the generated code.
    /// </summary>
    public string? RootNamespace { get; } = rootNamespace;

    /// <summary>
    /// Gets a value that indicates whether to suppress the default <c>#pragma checksum</c> directive in the
    /// generated C# code. If <c>false</c> the checksum directive will be included, otherwise it will not be
    /// generated. Defaults to <c>false</c>, meaning that the checksum will be included.
    /// </summary>
    /// <remarks>
    /// The <c>#pragma checksum</c> is required to enable debugging and should only be suppressed for testing
    /// purposes.
    /// </remarks>
    public bool SuppressChecksum { get; } = suppressChecksum;

    /// <summary>
    /// Gets a value that indicates whether to suppress the default metadata attributes in the generated
    /// C# code. If <c>false</c> the default attributes will be included, otherwise they will not be generated.
    /// Defaults to <c>false</c> at run time, meaning that the attributes will be included. Defaults to
    /// <c>true</c> at design time, meaning that the attributes will not be included.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>Microsoft.AspNetCore.Razor.Runtime</c> package includes a default set of attributes intended
    /// for runtimes to discover metadata about the compiled code.
    /// </para>
    /// <para>
    /// The default metadata attributes should be suppressed if code generation targets a runtime without
    /// a reference to <c>Microsoft.AspNetCore.Razor.Runtime</c>, or for testing purposes.
    /// </para>
    /// </remarks>
    public bool SuppressMetadataAttributes { get; } = suppressMetadataAttributes;

    /// <summary>
    /// Gets a value that indicates whether to suppress the <c>RazorSourceChecksumAttribute</c>.
    /// <para>
    /// Used by default in .NET 6 apps since including a type-level attribute that changes on every
    /// edit are treated as rude edits by hot reload.
    /// </para>
    /// </summary>
    public bool SuppressMetadataSourceChecksumAttributes { get; } = suppressMetadataSourceChecksumAttributes;

    /// <summary>
    /// Gets or sets a value that determines if an empty body is generated for the primary method.
    /// </summary>
    public bool SuppressPrimaryMethodBody { get; } = suppressPrimaryMethodBody;

    /// <summary>
    /// Gets a value that determines if nullability type enforcement should be suppressed for user code.
    /// </summary>
    public bool SuppressNullabilityEnforcement { get; } = suppressNullabilityEnforcement;

    /// <summary>
    /// Gets a value that determines if the components code writer may omit values for minimized attributes.
    /// </summary>
    public bool OmitMinimizedComponentAttributeValues { get; } = omitMinimizedComponentAttributeValues;

    /// <summary>
    /// Gets a value that determines if localized component names are to be supported.
    /// </summary>
    public bool SupportLocalizedComponentNames { get; set; } = supportLocalizedComponentNames;

    /// <summary>
    /// Gets a value that determines if enhanced line pragmas are to be utilized.
    /// </summary>
    public bool UseEnhancedLinePragma { get; } = useEnhancedLinePragma;

    /// <summary>
    /// Gets a value used for unique ids for testing purposes. Null for unique ids.
    /// </summary>
    public string? SuppressUniqueIds { get; } = suppressUniqueIds;

    /// <summary>
    /// Determines whether RenderTreeBuilder.AddComponentParameter should not be used.
    /// </summary>
    public bool SuppressAddComponentParameter { get; } = suppressAddComponentParameter;

    /// <summary>
    /// Determines if the file paths emitted as part of line pragmas should be mapped back to a valid path on windows.
    /// </summary>
    public bool RemapLinePragmaPathsOnWindows { get; } = remapLinePragmaPathsOnWindows;

    public static RazorCodeGenerationOptions Default { get; } = new RazorCodeGenerationOptions(
        indentWithTabs: false,
        indentSize: 4,
        designTime: false,
        suppressChecksum: false,
        rootNamespace: null,
        suppressMetadataAttributes: false,
        suppressMetadataSourceChecksumAttributes: false,
        suppressPrimaryMethodBody: false,
        suppressNullabilityEnforcement: false,
        omitMinimizedComponentAttributeValues: false,
        supportLocalizedComponentNames: false,
        useEnhancedLinePragma: true,
        suppressUniqueIds: null,
        suppressAddComponentParameter: false,
        remapLinePragmaPathsOnWindows: false);

    public static RazorCodeGenerationOptions DesignTimeDefault { get; } = new RazorCodeGenerationOptions(
        indentWithTabs: false,
        indentSize: 4,
        designTime: true,
        rootNamespace: null,
        suppressChecksum: false,
        suppressMetadataAttributes: true,
        suppressMetadataSourceChecksumAttributes: false,
        suppressPrimaryMethodBody: false,
        suppressNullabilityEnforcement: false,
        omitMinimizedComponentAttributeValues: false,
        supportLocalizedComponentNames: false,
        useEnhancedLinePragma: true,
        suppressUniqueIds: null,
        suppressAddComponentParameter: false,
        remapLinePragmaPathsOnWindows: true);

    public static RazorCodeGenerationOptions Create(Action<RazorCodeGenerationOptionsBuilder> configure)
    {
        ArgHelper.ThrowIfNull(configure);

        var builder = new RazorCodeGenerationOptionsBuilder(designTime: false);
        configure(builder);
        var options = builder.Build();

        return options;
    }

    public static RazorCodeGenerationOptions CreateDesignTime(Action<RazorCodeGenerationOptionsBuilder> configure)
    {
        ArgHelper.ThrowIfNull(configure);

        var builder = new RazorCodeGenerationOptionsBuilder(designTime: true)
        {
            SuppressMetadataAttributes = true,
        };

        configure(builder);
        var options = builder.Build();

        return options;
    }
}
