// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.CodeAnalysis.Razor.Protocol;

internal static class JsonHelpers
{
    private const string s_convertedFlag = "__convertedFromJObject";

    /// <summary>
    /// Normalizes data from JObject to JsonElement as thats what we expect to process
    /// </summary>
    internal static object? TryConvertFromJObject(object? data)
    {
        if (data is JObject jObject)
        {
            jObject[s_convertedFlag] = true;
            return JsonDocument.Parse(jObject.ToString()).RootElement;
            //return JsonSerializer.SerializeToElement(JsonNode.Parse(jObject.ToString()));
        }

        return data;
    }

    /// <summary>
    /// Converts from JObject back to JsonElement, but only if the original conversion was done with <see cref="TryConvertFromJsonElement(object?)"/>
    /// </summary>
    internal static object? TryConvertBackToJObject(object? data)
    {
        if (codeAction.Data is JsonElement jsonElement &&
            jsonElement.TryGetProperty(s_convertedFlag, out _))
        {
            codeAction.Data = JObject.Parse(jsonElement.ToString());
        }

        return data;
    }
}
