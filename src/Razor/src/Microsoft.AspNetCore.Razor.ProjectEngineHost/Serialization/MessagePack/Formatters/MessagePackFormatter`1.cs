// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using MessagePack;
using MessagePack.Formatters;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters;

internal abstract partial class MessagePackFormatter<T> : IMessagePackFormatter<T>
{
    private static readonly StringInterningFormatter s_stringFormatter = new();

    public abstract T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options);
    public abstract void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options);

    public static string DeserializeString(ref MessagePackReader reader, MessagePackSerializerOptions options)
        => s_stringFormatter.Deserialize(ref reader, options).AssumeNotNull();

    public static string? DeserializeStringOrNull(ref MessagePackReader reader, MessagePackSerializerOptions options)
        => s_stringFormatter.Deserialize(ref reader, options);
}
