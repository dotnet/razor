// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using MessagePack;
using MessagePack.Formatters;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters;

/// <summary>
///  A message package formatter that cannot be serialized at the top-level. It expects a
///  <see cref="SerializerCachingOptions"/> instance to be passed to it.
/// </summary>
internal abstract partial class ValueFormatter<T> : IMessagePackFormatter<T>
{
    public static readonly Type TargetType = typeof(T);

    public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (options is SerializerCachingOptions cachingOptions)
        {
            return Deserialize(ref reader, cachingOptions);
        }

        ThrowMissingCachingOptions();

        return default!;
    }

    public void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options)
    {
        if (options is SerializerCachingOptions cachingOptions)
        {
            Serialize(ref writer, value, cachingOptions);
            return;
        }

        ThrowMissingCachingOptions();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowMissingCachingOptions()
    {
        throw new InvalidOperationException($"Expected to be given a {nameof(SerializerCachingOptions)} instance.");
    }

    protected abstract T Deserialize(ref MessagePackReader reader, SerializerCachingOptions options);
    protected abstract void Serialize(ref MessagePackWriter writer, T value, SerializerCachingOptions options);
}
