// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using MessagePack;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters.TagHelpers;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters;

internal sealed class ProjectWorkspaceStateFormatter : MessagePackFormatter<ProjectWorkspaceState>
{
    public static readonly MessagePackFormatter<ProjectWorkspaceState> Instance = new ProjectWorkspaceStateFormatter();

    private ProjectWorkspaceStateFormatter()
    {
    }

    public override ProjectWorkspaceState Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        reader.ReadArrayHeaderAndVerify(2);

        using var cachingOptions = new CachingOptions(options);

        var tagHelpers = reader.Deserialize<ImmutableArray<TagHelperDescriptor>>(cachingOptions);
        var csharpLanguageVersion = (LanguageVersion)reader.ReadInt32();

        return new ProjectWorkspaceState(tagHelpers, csharpLanguageVersion);
    }

    public override void Serialize(ref MessagePackWriter writer, ProjectWorkspaceState value, MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(2);

        using var cachingOptions = new CachingOptions(options);

        writer.SerializeObject(value.TagHelpers, cachingOptions);
        writer.Write((int)value.CSharpLanguageVersion);
    }
}
