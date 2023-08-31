// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using MessagePack;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters.TagHelpers;

internal sealed class DocumentationObjectFormatter : MessagePackFormatter<DocumentationObject>
{
    public static readonly MessagePackFormatter<DocumentationObject> Instance = new DocumentationObjectFormatter();

    private DocumentationObjectFormatter()
    {
    }

    public override DocumentationObject Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return default;
        }

        var cache = options as CachingOptions;

        var count = reader.ReadArrayHeader();

        var documentationKind = (DocumentationKind)reader.ReadInt32();

        switch (documentationKind)
        {
            case DocumentationKind.Descriptor:
                var id = (DocumentationId)reader.ReadInt32();

                count -= 2;

                // Note: Each argument is stored as two values.
                var args = count > 0
                    ? ReadArgs(ref reader, count / 2, cache)
                    : Array.Empty<object>();

                return DocumentationDescriptor.From(id, args);

            case DocumentationKind.String:
                if (count != 2)
                {
                    throw new MessagePackSerializationException($"Expected array of 2 elements for string documentation, but it was {count}.");
                }

                return reader.ReadString(cache).AssumeNotNull();

            default:
                throw new NotSupportedException(SR.FormatUnsupported_argument_kind(documentationKind));
        }
    }

    private static object?[] ReadArgs(ref MessagePackReader reader, int count, CachingOptions? cache)
    {
        using var builder = new PooledArrayBuilder<object?>(capacity: count);

        for (var i = 0; i < count; i++)
        {
            var item = ReadArg(ref reader, cache);
            builder.Add(item);
        }

        return builder.ToArray();
    }

    private static object? ReadArg(ref MessagePackReader reader, CachingOptions? cache)
    {
        if (reader.TryReadNil())
        {
            return null;
        }

        var argKind = (ArgKind)reader.ReadInt32();

        return argKind switch
        {
            ArgKind.String => reader.ReadString(cache),
            ArgKind.Integer => reader.ReadInt32(),
            ArgKind.Boolean => reader.ReadBoolean(),

            _ => throw new NotSupportedException(SR.FormatUnsupported_argument_kind(argKind)),
        };
    }

    public override void Serialize(ref MessagePackWriter writer, DocumentationObject value, MessagePackSerializerOptions options)
    {
        var cache = options as CachingOptions;

        switch (value.Object)
        {
            case DocumentationDescriptor descriptor:
                var args = descriptor.Args;
                var count = 2 + (args.Length * 2);

                writer.WriteArrayHeader(count);

                writer.Write((int)DocumentationKind.Descriptor);
                writer.Write((int)descriptor.Id);

                foreach (var arg in args)
                {
                    WriteArg(ref writer, arg, cache);
                }

                break;

            case string text:
                writer.WriteArrayHeader(2);
                writer.Write((int)DocumentationKind.String);
                writer.Write(text, cache);
                break;

            case null:
                writer.WriteNil();
                break;

            default:
                Debug.Fail($"Documentation objects should only be of type {nameof(DocumentationDescriptor)}, string, or null.");
                break;
        }

        static void WriteArg(ref MessagePackWriter writer, object? value, CachingOptions? cache)
        {
            switch (value)
            {
                case string s:
                    writer.Write((int)ArgKind.String);
                    writer.Write(s, cache);
                    break;

                case int i:
                    writer.Write((int)ArgKind.Integer);
                    writer.Write(i);
                    break;

                case bool b:
                    writer.Write((int)ArgKind.Boolean);
                    writer.Write(b);
                    break;

                case null:
                    writer.Write((int)ArgKind.String);
                    writer.WriteNil();
                    break;

                case var arg:
                    ThrowNotSupported(arg.GetType());
                    break;
            }

            [DoesNotReturn]
            static void ThrowNotSupported(Type type)
            {
                throw new NotSupportedException(
                    SR.FormatUnsupported_argument_type(type.FullName));
            }
        }
    }
}
