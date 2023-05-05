// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal static partial class ObjectReaders
{
    private record struct ProjectWorkspaceStateData(TagHelperDescriptor[] TagHelpers, LanguageVersion CSharpLanguageVersion)
    {
        public static readonly PropertyMap<ProjectWorkspaceStateData> PropertyMap = new(
            (nameof(TagHelpers), ReadTagHelpers),
            (nameof(CSharpLanguageVersion), ReadCSharpLanguageVersion));

        private static void ReadTagHelpers(JsonReader reader, ref ProjectWorkspaceStateData data)
            => data.TagHelpers = reader.ReadArrayOrEmpty(static reader => ReadTagHelper(reader, useCache: true));

        private static void ReadCSharpLanguageVersion(JsonReader reader, ref ProjectWorkspaceStateData data)
            => data.CSharpLanguageVersion = (LanguageVersion)reader.ReadInt32();
    }
}
