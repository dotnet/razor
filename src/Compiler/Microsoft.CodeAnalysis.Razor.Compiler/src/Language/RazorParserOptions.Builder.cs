// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed partial class RazorParserOptions
{
    public sealed class Builder
    {
        public RazorLanguageVersion LanguageVersion { get; }
        public string FileKind { get; }

        private Flags _flags;

        public ImmutableArray<DirectiveDescriptor> Directives { get; set => field = value.NullToEmpty(); }
        public CSharpParseOptions CSharpParseOptions { get; set => field = value ?? CSharpParseOptions.Default; }

        internal RazorParserFeatureFlags FeatureFlags { get; set; }

        internal Builder(RazorLanguageVersion languageVersion, string fileKind)
        {
            LanguageVersion = languageVersion ?? RazorLanguageVersion.Latest;
            FileKind = fileKind ?? FileKinds.Legacy;
            Directives = [];
            CSharpParseOptions = CSharpParseOptions.Default;
            FeatureFlags = RazorParserFeatureFlags.Create(LanguageVersion, FileKind);
        }

        public bool DesignTime
        {
            get => _flags.IsFlagSet(Flags.DesignTime);
            set => _flags.UpdateFlag(Flags.DesignTime, value);
        }

        public bool ParseLeadingDirectives
        {
            get => _flags.IsFlagSet(Flags.ParseLeadingDirectives);
            set => _flags.UpdateFlag(Flags.ParseLeadingDirectives, value);
        }

        public bool UseRoslynTokenizer
        {
            get => _flags.IsFlagSet(Flags.UseRoslynTokenizer);
            set => _flags.UpdateFlag(Flags.UseRoslynTokenizer, value);
        }

        internal bool EnableSpanEditHandlers
        {
            get => _flags.IsFlagSet(Flags.EnableSpanEditHandlers);
            set => _flags.UpdateFlag(Flags.EnableSpanEditHandlers, value);
        }

        public RazorParserOptions ToOptions()
            => new(LanguageVersion, FileKind, Directives, CSharpParseOptions, FeatureFlags, _flags);
    }
}
