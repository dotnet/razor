// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal static partial class ObjectReaders
{
    private record struct SourceSpanData(string? FilePath, int AbsoluteIndex, int LineIndex, int CharacterIndex, int Length)
    {
        public static readonly PropertyMap<SourceSpanData> PropertyMap = new(
            (nameof(SourceSpanData.FilePath), ReadFilePath),
            (nameof(SourceSpanData.AbsoluteIndex), ReadAbsoluteIndex),
            (nameof(SourceSpanData.LineIndex), ReadLineIndex),
            (nameof(SourceSpanData.CharacterIndex), ReadCharacterIndex),
            (nameof(SourceSpanData.Length), ReadLength));

        public static void ReadFilePath(JsonReader reader, ref SourceSpanData data)
            => data.FilePath = reader.ReadString();

        public static void ReadAbsoluteIndex(JsonReader reader, ref SourceSpanData data)
            => data.AbsoluteIndex = reader.ReadInt32();

        public static void ReadLineIndex(JsonReader reader, ref SourceSpanData data)
            => data.LineIndex = reader.ReadInt32();

        public static void ReadCharacterIndex(JsonReader reader, ref SourceSpanData data)
            => data.CharacterIndex = reader.ReadInt32();

        public static void ReadLength(JsonReader reader, ref SourceSpanData data)
            => data.Length = reader.ReadInt32();
    }
}
