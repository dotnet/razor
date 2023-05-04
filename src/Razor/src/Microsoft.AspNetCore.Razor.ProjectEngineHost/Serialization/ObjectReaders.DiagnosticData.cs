// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal static partial class ObjectReaders
{
    private record struct DiagnosticData(string Id, RazorDiagnosticSeverity Severity, string Message, SourceSpan Span)
    {
        public static readonly PropertyMap<DiagnosticData> PropertyMap = new(
            (nameof(DiagnosticData.Id), ReadId),
            (nameof(DiagnosticData.Severity), ReadSeverity),
            (nameof(DiagnosticData.Message), ReadMessage),
            (nameof(DiagnosticData.Span), ReadSpan));

        public static void ReadId(JsonReader reader, ref DiagnosticData data)
            => data.Id = reader.ReadNonNullString();

        public static void ReadSeverity(JsonReader reader, ref DiagnosticData data)
            => data.Severity = (RazorDiagnosticSeverity)reader.ReadInt32();

        public static void ReadMessage(JsonReader reader, ref DiagnosticData data)
            => data.Message = reader.ReadNonNullString();

        public static void ReadSpan(JsonReader reader, ref DiagnosticData data)
        {
            SourceSpanData span = default;
            reader.ReadObjectData(ref span, SourceSpanData.PropertyMap);
            data.Span = new SourceSpan(span.FilePath, span.AbsoluteIndex, span.LineIndex, span.CharacterIndex, span.Length);
        }
    }
}
