﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.ProjectSystem;

public sealed class ProjectWorkspaceState : IEquatable<ProjectWorkspaceState>
{
    public static readonly ProjectWorkspaceState Default = new(ImmutableArray<TagHelperDescriptor>.Empty, LanguageVersion.Default);

    public ImmutableArray<TagHelperDescriptor> TagHelpers { get; }
    public LanguageVersion CSharpLanguageVersion { get; }

    public ProjectWorkspaceState(
        ImmutableArray<TagHelperDescriptor> tagHelpers,
        LanguageVersion csharpLanguageVersion)
    {
        TagHelpers = tagHelpers;
        CSharpLanguageVersion = csharpLanguageVersion;
    }

    public override bool Equals(object? obj)
        => obj is ProjectWorkspaceState other && Equals(other);

    public bool Equals(ProjectWorkspaceState? other)
        => other is not null &&
           TagHelpers.SequenceEqual(other.TagHelpers) &&
           CSharpLanguageVersion == other.CSharpLanguageVersion;

    public override int GetHashCode()
    {
        var hash = HashCodeCombiner.Start();

        foreach (var tagHelper in TagHelpers)
        {
            hash.Add(tagHelper.GetHashCode());
        }

        hash.Add(CSharpLanguageVersion);

        return hash.CombinedHash;
    }
}
