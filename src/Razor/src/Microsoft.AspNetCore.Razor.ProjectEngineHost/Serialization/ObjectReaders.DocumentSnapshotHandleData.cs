// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal static partial class ObjectReaders
{
    private record struct DocumentSnapshotHandleData(string FilePath, string TargetPath, string FileKind)
    {
        public static readonly PropertyMap<DocumentSnapshotHandleData> PropertyMap = new(
            (nameof(FilePath), ReadFilePath),
            (nameof(TargetPath), ReadTargetPath),
            (nameof(FileKind), ReadFileKind));

        private static void ReadFilePath(JsonReader reader, ref DocumentSnapshotHandleData data)
            => data.FilePath = reader.ReadNonNullString();

        private static void ReadTargetPath(JsonReader reader, ref DocumentSnapshotHandleData data)
            => data.TargetPath = reader.ReadNonNullString();

        private static void ReadFileKind(JsonReader reader, ref DocumentSnapshotHandleData data)
            => data.FileKind = reader.ReadNonNullString();
    }
}
