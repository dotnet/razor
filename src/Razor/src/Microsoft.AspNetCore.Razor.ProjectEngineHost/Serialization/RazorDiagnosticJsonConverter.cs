// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal class RazorDiagnosticJsonConverter : ObjectJsonConverter<RazorDiagnostic>
{
    private const string RazorDiagnosticMessageKey = "Message";

    public static readonly RazorDiagnosticJsonConverter Instance = new();

    private RazorDiagnosticJsonConverter()
    {
    }

    protected override RazorDiagnostic ReadFromProperties(JsonReader reader)
    {
        DiagnosticData data = default;
        reader.ReadProperties(ref data, DiagnosticData.PropertyMap);

        var descriptor = new RazorDiagnosticDescriptor(data.Id, MessageFormat(data.Message), data.Severity);

        return RazorDiagnostic.Create(descriptor, data.Span);

        static Func<string> MessageFormat(string message)
        {
            return () => message;
        }
    }

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

    protected override void WriteProperties(JsonWriter writer, RazorDiagnostic value)
    {
        writer.Write(nameof(value.Id), value.Id);
        writer.Write(nameof(value.Severity), (int)value.Severity);
        writer.Write(RazorDiagnosticMessageKey, value.GetMessage(CultureInfo.CurrentCulture));
        writer.WriteObject(nameof(value.Span), value.Span, static (writer, value) =>
        {
            writer.Write(nameof(value.FilePath), value.FilePath);
            writer.Write(nameof(value.AbsoluteIndex), value.AbsoluteIndex);
            writer.Write(nameof(value.LineIndex), value.LineIndex);
            writer.Write(nameof(value.CharacterIndex), value.CharacterIndex);
            writer.Write(nameof(value.Length), value.Length);
        });
    }
}
