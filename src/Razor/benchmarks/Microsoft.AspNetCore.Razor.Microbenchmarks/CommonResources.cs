// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.IO;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Serialization.Json;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks;

internal static class CommonResources
{
    public static readonly byte[] LegacyTagHelperBytes = Resources.GetResourceBytes("taghelpers.json");
    public static readonly ImmutableArray<TagHelperDescriptor> LegacyTagHelpers = LoadTagHelpers(LegacyTagHelperBytes);

    public static readonly byte[] LegacyProjectRazorJsonBytes = Resources.GetResourceBytes("project.razor.json");
    public static readonly RazorProjectInfo LegacyProjectInfo = LoadProjectInfo(LegacyProjectRazorJsonBytes);

    public static readonly byte[] TelerikTagHelperBytes = Resources.GetResourceBytes("Kendo.Mvc.Examples.taghelpers.json", folder: "Telerik");
    public static readonly ImmutableArray<TagHelperDescriptor> TelerikTagHelpers = LoadTagHelpers(TelerikTagHelperBytes);

    public static readonly byte[] TelerikProjectRazorJsonBytes = Resources.GetResourceBytes("Kendo.Mvc.Examples.project.razor.json", folder: "Telerik");
    public static readonly RazorProjectInfo TelerikProjectInfo = LoadProjectInfo(TelerikProjectRazorJsonBytes);

    private static ImmutableArray<TagHelperDescriptor> LoadTagHelpers(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new StreamReader(stream);

        return JsonDataConvert.DeserializeData(reader,
            static r => r.ReadImmutableArray(
                static r => ObjectReaders.ReadTagHelper(r, useCache: false))).NullToEmpty();
    }

    private static RazorProjectInfo LoadProjectInfo(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new StreamReader(stream);

        return JsonDataConvert.DeserializeData(reader,
            static r => r.ReadNonNullObject(ObjectReaders.ReadRazorProjectInfoFromProperties));
    }
}
