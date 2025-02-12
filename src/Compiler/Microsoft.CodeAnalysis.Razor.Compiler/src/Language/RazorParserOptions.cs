// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed partial class RazorParserOptions
{
    public static RazorParserOptions Default { get; } = new(
        languageVersion: RazorLanguageVersion.Latest,
        fileKind: FileKinds.Legacy,
        directives: [],
        csharpParseOptions: CSharpParseOptions.Default,
        featureFlags: RazorParserFeatureFlags.Create(RazorLanguageVersion.Latest, FileKinds.Legacy),
        flags: 0);

    private readonly Flags _flags;

    public RazorLanguageVersion LanguageVersion { get; } = RazorLanguageVersion.Latest;
    internal string FileKind { get; }

    public ImmutableArray<DirectiveDescriptor> Directives { get; }
    public CSharpParseOptions CSharpParseOptions { get; }
    internal RazorParserFeatureFlags FeatureFlags { get; }

    private RazorParserOptions(
        RazorLanguageVersion languageVersion,
        string fileKind,
        ImmutableArray<DirectiveDescriptor> directives,
        CSharpParseOptions csharpParseOptions,
        RazorParserFeatureFlags featureFlags,
        Flags flags)
    {
        if (flags.IsFlagSet(Flags.ParseLeadingDirectives) &&
            flags.IsFlagSet(Flags.UseRoslynTokenizer))
        {
            throw new ArgumentException($"Cannot set {nameof(Flags.ParseLeadingDirectives)} and {nameof(Flags.UseRoslynTokenizer)} to true simultaneously.");
        }

        LanguageVersion = languageVersion ?? RazorLanguageVersion.Latest;
        FileKind = fileKind ?? FileKinds.Legacy;
        Directives = directives;
        CSharpParseOptions = csharpParseOptions;
        _flags = flags;
        FeatureFlags = featureFlags;
    }

    public bool DesignTime
        => _flags.IsFlagSet(Flags.DesignTime);

    /// <summary>
    /// Gets a value which indicates whether the parser will parse only the leading directives. If <c>true</c>
    /// the parser will halt at the first HTML content or C# code block. If <c>false</c> the whole document is parsed.
    /// </summary>
    /// <remarks>
    /// Currently setting this option to <c>true</c> will result in only the first line of directives being parsed.
    /// In a future release this may be updated to include all leading directive content.
    /// </remarks>
    public bool ParseLeadingDirectives
        => _flags.IsFlagSet(Flags.ParseLeadingDirectives);

    public bool UseRoslynTokenizer
        => _flags.IsFlagSet(Flags.UseRoslynTokenizer);

    internal bool EnableSpanEditHandlers
        => _flags.IsFlagSet(Flags.EnableSpanEditHandlers);

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
            flags.UpdateFlag(Flags.DesignTime, designTime.Value);
        }

        if (parseLeadingDirectives.HasValue)
        {
            flags.UpdateFlag(Flags.ParseLeadingDirectives, parseLeadingDirectives.Value);
        }

        if (useRoslynTokenizer.HasValue)
        {
            flags.UpdateFlag(Flags.UseRoslynTokenizer, useRoslynTokenizer.Value);
        }

        if (enableSpanEditHandlers.HasValue)
        {
            flags.UpdateFlag(Flags.EnableSpanEditHandlers, enableSpanEditHandlers.Value);
        }

        return flags == _flags
            ? this
            : new(LanguageVersion, FileKind, Directives, CSharpParseOptions, FeatureFlags, flags);
    }
}
