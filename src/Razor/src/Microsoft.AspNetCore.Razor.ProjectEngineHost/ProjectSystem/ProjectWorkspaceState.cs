// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.ProjectSystem;

internal sealed class ProjectWorkspaceState : IEquatable<ProjectWorkspaceState>
{
    public static readonly ProjectWorkspaceState Default = new(ImmutableArray<TagHelperDescriptor>.Empty, useRoslynTokenizer: false, LanguageVersion.Default);

    public ImmutableArray<TagHelperDescriptor> TagHelpers { get; }
    public LanguageVersion CSharpLanguageVersion { get; }
    public bool UseRoslynTokenizer { get; }

    private ProjectWorkspaceState(
        ImmutableArray<TagHelperDescriptor> tagHelpers,
        bool useRoslynTokenizer,
        LanguageVersion csharpLanguageVersion)
    {
        TagHelpers = tagHelpers;
        CSharpLanguageVersion = csharpLanguageVersion;
        UseRoslynTokenizer = useRoslynTokenizer;
    }

    public static ProjectWorkspaceState Create(
        ImmutableArray<TagHelperDescriptor> tagHelpers,
        bool useRoslynTokenizer,
        LanguageVersion csharpLanguageVersion = LanguageVersion.Default)
        => tagHelpers.IsEmpty && csharpLanguageVersion == LanguageVersion.Default
            ? Default
            : new(tagHelpers, useRoslynTokenizer, csharpLanguageVersion);

    public static ProjectWorkspaceState Create(LanguageVersion csharpLanguageVersion, bool useRoslynTokenizer)
        => csharpLanguageVersion == LanguageVersion.Default
            ? Default
            : new(ImmutableArray<TagHelperDescriptor>.Empty, useRoslynTokenizer, csharpLanguageVersion);

    public override bool Equals(object? obj)
        => obj is ProjectWorkspaceState other && Equals(other);

    public bool Equals(ProjectWorkspaceState? other)
        => other is not null &&
           TagHelpers.SequenceEqual(other.TagHelpers) &&
           CSharpLanguageVersion == other.CSharpLanguageVersion &&
           UseRoslynTokenizer == other.UseRoslynTokenizer;

    public override int GetHashCode()
    {
        var hash = HashCodeCombiner.Start();

        hash.Add(TagHelpers);
        hash.Add(CSharpLanguageVersion);
        hash.Add(UseRoslynTokenizer);

        return hash.CombinedHash;
    }
}
