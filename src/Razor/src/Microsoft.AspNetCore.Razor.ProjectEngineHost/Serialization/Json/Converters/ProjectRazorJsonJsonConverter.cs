// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.


/* Unmerged change from project 'Microsoft.AspNetCore.Razor.ProjectEngineHost (net8.0)'
Before:
using Microsoft.AspNetCore.Razor.ProjectSystem;
After:
using Microsoft;
using Microsoft.AspNetCore.Razor;

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.ProjectSystem;
*/
using Microsoft.AspNetCore.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.Serialization.Json.Converters;

internal class ProjectRazorJsonJsonConverter : ObjectJsonConverter<ProjectRazorJson>
{
    public static readonly ProjectRazorJsonJsonConverter Instance = new();

    private ProjectRazorJsonJsonConverter()
    {
    }

    protected override ProjectRazorJson ReadFromProperties(JsonDataReader reader)
        => ObjectReaders.ReadProjectRazorJsonFromProperties(reader);

    protected override void WriteProperties(JsonDataWriter writer, ProjectRazorJson value)
        => ObjectWriters.WriteProperties(writer, value);
}
