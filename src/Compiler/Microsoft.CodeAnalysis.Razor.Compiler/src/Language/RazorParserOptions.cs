// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class RazorParserOptions
{
    public static RazorParserOptions Default { get; } = new(
        languageVersion: RazorLanguageVersion.Latest,
        fileKind: FileKinds.Legacy,
        directives: [],
        csharpParseOptions: CSharpParseOptions.Default,
        featureFlags: RazorParserFeatureFlags.Create(RazorLanguageVersion.Latest, FileKinds.Legacy),
        flags: 0);

    private readonly RazorParserOptionsFlags _flags;

    public RazorLanguageVersion LanguageVersion { get; } = RazorLanguageVersion.Latest;
    internal string FileKind { get; }

    public ImmutableArray<DirectiveDescriptor> Directives { get; }
    public CSharpParseOptions CSharpParseOptions { get; }
    internal RazorParserFeatureFlags FeatureFlags { get; }

    internal RazorParserOptions(
        RazorLanguageVersion languageVersion,
        string fileKind,
        ImmutableArray<DirectiveDescriptor> directives,
        CSharpParseOptions csharpParseOptions,
        RazorParserFeatureFlags featureFlags,
        RazorParserOptionsFlags flags)
    {
        if (flags.IsFlagSet(RazorParserOptionsFlags.ParseLeadingDirectives) &&
            flags.IsFlagSet(RazorParserOptionsFlags.UseRoslynTokenizer))
        {
            throw new ArgumentException($"Cannot set {nameof(RazorParserOptionsFlags.ParseLeadingDirectives)} and {nameof(RazorParserOptionsFlags.UseRoslynTokenizer)} to true simultaneously.");
        }

        LanguageVersion = languageVersion ?? RazorLanguageVersion.Latest;
        FileKind = fileKind ?? FileKinds.Legacy;
        Directives = directives;
        CSharpParseOptions = csharpParseOptions;
        _flags = flags;
        FeatureFlags = featureFlags;
    }

    public bool DesignTime
        => _flags.IsFlagSet(RazorParserOptionsFlags.DesignTime);

    /// <summary>
    /// Gets a value which indicates whether the parser will parse only the leading directives. If <c>true</c>
    /// the parser will halt at the first HTML content or C# code block. If <c>false</c> the whole document is parsed.
    /// </summary>
    /// <remarks>
    /// Currently setting this option to <c>true</c> will result in only the first line of directives being parsed.
    /// In a future release this may be updated to include all leading directive content.
    /// </remarks>
    public bool ParseLeadingDirectives
        => _flags.IsFlagSet(RazorParserOptionsFlags.ParseLeadingDirectives);

    public bool UseRoslynTokenizer
        => _flags.IsFlagSet(RazorParserOptionsFlags.UseRoslynTokenizer);

    internal bool EnableSpanEditHandlers
        => _flags.IsFlagSet(RazorParserOptionsFlags.EnableSpanEditHandlers);

    public RazorParserOptions WithDirectives(params ImmutableArray<DirectiveDescriptor> value)
        => Directives.SequenceEqual(value)
            ? this
            : new(LanguageVersion, FileKind, value, CSharpParseOptions, FeatureFlags, _flags);

    public RazorParserOptions WithCSharpParseOptions(CSharpParseOptions value)
        => CSharpParseOptions.Equals(value)
            ? this
            : new(LanguageVersion, FileKind, Directives, value, FeatureFlags, _flags);

    public RazorParserOptions WithFlags(
        Optional<bool> designTime = default,
        Optional<bool> parseLeadingDirectives = default,
        Optional<bool> useRoslynTokenizer = default,
        Optional<bool> enableSpanEditHandlers = default)
    {
        var flags = _flags;

        if (designTime.HasValue)
        {
            flags.UpdateFlag(RazorParserOptionsFlags.DesignTime, designTime.Value);
        }

        if (parseLeadingDirectives.HasValue)
        {
            flags.UpdateFlag(RazorParserOptionsFlags.ParseLeadingDirectives, parseLeadingDirectives.Value);
        }

        if (useRoslynTokenizer.HasValue)
        {
            flags.UpdateFlag(RazorParserOptionsFlags.UseRoslynTokenizer, useRoslynTokenizer.Value);
        }

        if (enableSpanEditHandlers.HasValue)
        {
            flags.UpdateFlag(RazorParserOptionsFlags.EnableSpanEditHandlers, enableSpanEditHandlers.Value);
        }

        return flags == _flags
            ? this
            : new(LanguageVersion, FileKind, Directives, CSharpParseOptions, FeatureFlags, flags);
    }
}
