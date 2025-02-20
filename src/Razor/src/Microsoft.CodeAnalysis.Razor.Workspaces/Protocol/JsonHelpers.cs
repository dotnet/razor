// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Newtonsoft.Json.Linq;

namespace Microsoft.CodeAnalysis.Razor.Protocol;

internal static class JsonHelpers
{
    private const string s_convertedFlag = "__convertedFromJObject";
    private static readonly Lazy<JsonSerializerOptions> s_roslynLspJsonSerializerOptions = new(CreateRoslynLspJsonSerializerOptions);

    /// <summary>
    /// Normalizes data from JObject to JsonElement as thats what we expect to process
    /// </summary>
    internal static object? TryConvertFromJObject(object? data)
    {
        if (data is JObject jObject)
        {
            jObject[s_convertedFlag] = true;
            return JsonDocument.Parse(jObject.ToString()).RootElement;
        }

        return data;
    }

    /// <summary>
    /// Converts from JObject back to JsonElement, but only if the original conversion was done with <see cref="TryConvertFromJObject(object?)"/>
    /// </summary>
    internal static object? TryConvertBackToJObject(object? data)
    {
        if (data is JsonElement jsonElement &&
            jsonElement.TryGetProperty(s_convertedFlag, out _))
        {
            data = JObject.Parse(jsonElement.ToString());
        }

        return data;
    }

    /// <summary>
    /// Serializer options to use when serializing or deserializing a Roslyn LSP type
    /// </summary>
    internal static JsonSerializerOptions RoslynLspJsonSerializerOptions => s_roslynLspJsonSerializerOptions.Value;

    /// <summary>
    /// Converts an LSP object to a different LSP object, either by casting or serializing and deserializing
    /// </summary>
    internal static TResult? Convert<TSource, TResult>(TSource? source)
    {
        if (source is TResult result)
        {
            return result;
        }

        return JsonSerializer.Deserialize<TResult>(JsonSerializer.SerializeToDocument(source, RoslynLspJsonSerializerOptions), RoslynLspJsonSerializerOptions);
    }

    /// <summary>
    /// Converts an array of LSP objects to a different LSP object, either by casting or serializing and deserializing
    /// </summary>
    internal static TResult[] ConvertAll<TSource, TResult>(TSource[] source)
    {
        using var results = new PooledArrayBuilder<TResult>(source.Length);
        foreach (var item in source)
        {
            if (Convert<TSource, TResult>(item) is { } converted)
            {
                results.Add(converted);
            }
            else
            {
                Debug.Fail("Could not convert item to expected type.");
            }
        }

        return results.ToArray();
    }

    private static JsonSerializerOptions CreateRoslynLspJsonSerializerOptions()
    {
        var serializerOptions = new JsonSerializerOptions();

        foreach (var converter in RazorServiceDescriptorsWrapper.GetLspConverters())
        {
            serializerOptions.Converters.Add(converter);
        }

        return serializerOptions;
    }
}
