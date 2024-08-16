// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using MessagePack.Resolvers;
using MessagePack;
using Microsoft.AspNetCore.Razor.Serialization.MessagePack.Resolvers;
using System.Buffers;
using System.IO;
using System.Threading;

namespace Microsoft.AspNetCore.Razor.Serialization;

internal static class RazorMessagePackSerializer
{
    private static readonly MessagePackSerializerOptions s_options = MessagePackSerializerOptions.Standard
        .WithResolver(CompositeResolver.Create(
            RazorProjectInfoResolver.Instance,
            StandardResolver.Instance));

    public static byte[] Serialize<T>(T instance)
        => MessagePackSerializer.Serialize(instance, s_options);

    public static void SerializeTo<T>(T instance, IBufferWriter<byte> bufferWriter)
        => MessagePackSerializer.Serialize(bufferWriter, instance, s_options);

    public static void SerializeTo<T>(T instance, Stream stream)
        => MessagePackSerializer.Serialize(stream, instance, s_options);

    public static T? DeserializeFrom<T>(ReadOnlyMemory<byte> buffer)
        => MessagePackSerializer.Deserialize<T>(buffer, s_options);

    public static T? DeserializeFrom<T>(Stream stream)
        => MessagePackSerializer.Deserialize<T>(stream, s_options);

    public static ValueTask<T> DeserializeFromAsync<T>(Stream stream, CancellationToken cancellationToken)
        => MessagePackSerializer.DeserializeAsync<T>(stream, s_options, cancellationToken);
}
