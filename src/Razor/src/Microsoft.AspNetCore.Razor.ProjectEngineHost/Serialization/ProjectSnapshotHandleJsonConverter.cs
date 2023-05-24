// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal partial class ProjectSnapshotHandleJsonConverter : ObjectJsonConverter<ProjectSnapshotHandle>
{
    public static readonly ProjectSnapshotHandleJsonConverter Instance = new();

    private ProjectSnapshotHandleJsonConverter()
    {
    }

    protected override ProjectSnapshotHandle ReadFromProperties(JsonDataReader reader)
        => ObjectReaders.ReadProjectSnapshotHandleFromProperties(reader);

    protected override void WriteProperties(JsonDataWriter writer, ProjectSnapshotHandle value)
        => ObjectWriters.WriteProperties(writer, value);
}
