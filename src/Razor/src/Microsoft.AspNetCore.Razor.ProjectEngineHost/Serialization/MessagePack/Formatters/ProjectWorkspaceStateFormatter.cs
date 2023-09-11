// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using MessagePack;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters;

internal sealed class ProjectWorkspaceStateFormatter : ValueFormatter<ProjectWorkspaceState>
{
    public static readonly ValueFormatter<ProjectWorkspaceState> Instance = new ProjectWorkspaceStateFormatter();

    private ProjectWorkspaceStateFormatter()
    {
    }

    protected override ProjectWorkspaceState Deserialize(ref MessagePackReader reader, SerializerCachingOptions options)
    {
        reader.ReadArrayHeaderAndVerify(2);

        var tagHelpers = reader.Deserialize<ImmutableArray<TagHelperDescriptor>>(options);
        var csharpLanguageVersion = (LanguageVersion)reader.ReadInt32();

        return new ProjectWorkspaceState(tagHelpers, csharpLanguageVersion);
    }

    protected override void Serialize(ref MessagePackWriter writer, ProjectWorkspaceState value, SerializerCachingOptions options)
    {
        writer.WriteArrayHeader(2);

        writer.SerializeObject(value.TagHelpers, options);
        writer.Write((int)value.CSharpLanguageVersion);
    }
}
