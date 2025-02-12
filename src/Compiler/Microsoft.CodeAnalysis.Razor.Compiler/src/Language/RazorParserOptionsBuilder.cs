// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class RazorParserOptionsBuilder
{
    public RazorLanguageVersion LanguageVersion { get; }
    public string FileKind { get; }

    private RazorParserOptionsFlags _flags;

    public ImmutableArray<DirectiveDescriptor> Directives { get; set => field = value.NullToEmpty(); }
    public CSharpParseOptions CSharpParseOptions { get; set => field = value ?? CSharpParseOptions.Default; }

    internal RazorParserFeatureFlags FeatureFlags { get; set; }

    internal RazorParserOptionsBuilder(RazorLanguageVersion languageVersion, string fileKind)
    {
        LanguageVersion = languageVersion ?? RazorLanguageVersion.Latest;
        FileKind = fileKind ?? FileKinds.Legacy;
        Directives = [];
        CSharpParseOptions = CSharpParseOptions.Default;
        FeatureFlags = RazorParserFeatureFlags.Create(LanguageVersion, FileKind);
    }

    public bool DesignTime
    {
        get => _flags.IsFlagSet(RazorParserOptionsFlags.DesignTime);
        set => _flags.UpdateFlag(RazorParserOptionsFlags.DesignTime, value);
    }

    public bool ParseLeadingDirectives
    {
        get => _flags.IsFlagSet(RazorParserOptionsFlags.ParseLeadingDirectives);
        set => _flags.UpdateFlag(RazorParserOptionsFlags.ParseLeadingDirectives, value);
    }

    public bool UseRoslynTokenizer
    {
        get => _flags.IsFlagSet(RazorParserOptionsFlags.UseRoslynTokenizer);
        set => _flags.UpdateFlag(RazorParserOptionsFlags.UseRoslynTokenizer, value);
    }

    internal bool EnableSpanEditHandlers
    {
        get => _flags.IsFlagSet(RazorParserOptionsFlags.EnableSpanEditHandlers);
        set => _flags.UpdateFlag(RazorParserOptionsFlags.EnableSpanEditHandlers, value);
    }

    public RazorParserOptions ToOptions()
        => new(LanguageVersion, FileKind, Directives, CSharpParseOptions, FeatureFlags, _flags);
}
