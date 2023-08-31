// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using MessagePack;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters;

internal sealed class ProjectSnapshotHandleFormatter : MessagePackFormatter<ProjectSnapshotHandle>
{
    public static readonly MessagePackFormatter<ProjectSnapshotHandle> Instance = new ProjectSnapshotHandleFormatter();

    private ProjectSnapshotHandleFormatter()
    {
    }

    public override ProjectSnapshotHandle Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        reader.ReadArrayHeaderAndVerify(3);

        var projectIdString = reader.DeserializeString(options);
        var configuration = reader.Deserialize<RazorConfiguration>(options);
        var rootNamespace = reader.DeserializeStringOrNull(options);

        var projectId = ProjectId.CreateFromSerialized(Guid.Parse(projectIdString));

        return new(projectId, configuration, rootNamespace);
    }

    public override void Serialize(ref MessagePackWriter writer, ProjectSnapshotHandle value, MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(3);

        writer.Write(value.ProjectId.Id.ToString());
        writer.Serialize(value.Configuration, options);
        writer.Write(value.RootNamespace);
    }
}
