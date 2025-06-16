// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MessagePack;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.Serialization.MessagePack.Formatters.TagHelpers;

internal sealed class TypeNameObjectFormatter : ValueFormatter<TypeNameObject>
{
    private const int PropertyCount = 2;

    public static readonly ValueFormatter<TypeNameObject> Instance = new TypeNameObjectFormatter();

    private TypeNameObjectFormatter()
    {
    }

    public override TypeNameObject Deserialize(ref MessagePackReader reader, SerializerCachingOptions options)
    {
        if (reader.TryReadNil())
        {
            return default;
        }

        reader.ReadArrayHeaderAndVerify(PropertyCount);

        var typeNameKind = (TypeNameKind)reader.ReadInt32();

        switch (typeNameKind)
        {
            case TypeNameKind.Index:
                var index = reader.ReadInt32();
                return new(index);
            case TypeNameKind.String:
                var fullName = CachedStringFormatter.Instance.Deserialize(ref reader, options).AssumeNotNull();
                return new(fullName);

            default:
                return Assumed.Unreachable<TypeNameObject>();
        }
    }

    public override void Serialize(ref MessagePackWriter writer, TypeNameObject value, SerializerCachingOptions options)
    {
        if (value.IsNull)
        {
            writer.WriteNil();
            return;
        }

        writer.WriteArrayHeader(PropertyCount);

        if (value.Index is int index)
        {
            writer.Write((int)TypeNameKind.Index);
            writer.Write(index);
        }
        else
        {
            writer.Write((int)TypeNameKind.String);
            CachedStringFormatter.Instance.Serialize(ref writer, value.GetTypeName(), options);
        }
    }

    public override void Skim(ref MessagePackReader reader, SerializerCachingOptions options)
    {
        if (reader.TryReadNil())
        {
            return;
        }

        reader.ReadArrayHeaderAndVerify(PropertyCount);

        reader.Skip(); // TypeNameKind
        CachedStringFormatter.Instance.Skim(ref reader, options); // Name
    }
}
