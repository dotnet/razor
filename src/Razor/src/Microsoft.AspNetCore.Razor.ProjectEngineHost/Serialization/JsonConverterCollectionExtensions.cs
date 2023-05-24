// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal static class JsonConverterCollectionExtensions
{
    private static readonly ImmutableArray<JsonConverter> s_converters = ImmutableArray.CreateRange(
        new JsonConverter[]
        {
            TagHelperResolutionResultJsonConverter.Instance,
            TagHelperDeltaResultJsonConverter.Instance,
            ProjectRazorJsonJsonConverter.Instance
        });

    public static void RegisterProjectSerializerConverters(this JsonConverterCollection collection)
    {
        if (collection is null)
        {
            throw new ArgumentNullException(nameof(collection));
        }

        foreach (var converter in s_converters)
        {
            collection.Add(converter);
        }
    }
}
