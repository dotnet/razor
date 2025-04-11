﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#if JSONSERIALIZATION_PROJECTSYSTEM
using System;
using System.IO;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.CodeAnalysis;
using SR = Microsoft.AspNetCore.Razor.Serialization.Json.Internal.Strings;

namespace Microsoft.AspNetCore.Razor.Serialization.Json;

internal static partial class ObjectReaders
{
    public static ProjectSnapshotHandle ReadProjectSnapshotHandleFromProperties(JsonDataReader reader)
    {
        var projectIdString = reader.ReadNonNullString(nameof(ProjectSnapshotHandle.ProjectId));
        var configuration = reader.ReadObjectOrNull(nameof(ProjectSnapshotHandle.Configuration), ReadConfigurationFromProperties) ?? RazorConfiguration.Default;
        var rootNamespace = reader.ReadStringOrNull(nameof(ProjectSnapshotHandle.RootNamespace));

        var projectId = ProjectId.CreateFromSerialized(Guid.Parse(projectIdString));

        return new(projectId, configuration, rootNamespace);
    }

    public static DocumentSnapshotHandle ReadDocumentSnapshotHandleFromProperties(JsonDataReader reader)
    {
        var filePath = reader.ReadNonNullString(nameof(DocumentSnapshotHandle.FilePath));
        var targetPath = reader.ReadNonNullString(nameof(DocumentSnapshotHandle.TargetPath));
        var fileKind = reader.ReadNonNullString(nameof(DocumentSnapshotHandle.FileKind));

        return new DocumentSnapshotHandle(filePath, targetPath, fileKind);
    }

    public static ProjectWorkspaceState ReadProjectWorkspaceStateFromProperties(JsonDataReader reader)
    {
        var tagHelpers = reader.ReadImmutableArrayOrEmpty(nameof(ProjectWorkspaceState.TagHelpers),
            static r => ReadTagHelper(r
#if JSONSERIALIZATION_ENABLETAGHELPERCACHE
                , useCache: true
#endif
            ));

        return ProjectWorkspaceState.Create(tagHelpers);
    }

    public static RazorProjectInfo ReadProjectInfoFromProperties(JsonDataReader reader)
    {
        if (!reader.TryReadInt32(WellKnownPropertyNames.Version, out var version) || version != SerializationFormat.Version)
        {
            throw new RazorProjectInfoSerializationException(SR.Unsupported_razor_project_info_version_encountered);
        }

        var projectKeyId = reader.ReadNonNullString(nameof(RazorProjectInfo.ProjectKey));
        var filePath = reader.ReadNonNullString(nameof(RazorProjectInfo.FilePath));
        var configuration = reader.ReadObject(nameof(RazorProjectInfo.Configuration), ReadConfigurationFromProperties) ?? RazorConfiguration.Default;
        var projectWorkspaceState = reader.ReadObject(nameof(RazorProjectInfo.ProjectWorkspaceState), ReadProjectWorkspaceStateFromProperties) ?? ProjectWorkspaceState.Default;
        var rootNamespace = reader.ReadString(nameof(RazorProjectInfo.RootNamespace));
        var documents = reader.ReadImmutableArray(nameof(RazorProjectInfo.Documents), static r => r.ReadNonNullObject(ReadDocumentSnapshotHandleFromProperties));

        var displayName = Path.GetFileNameWithoutExtension(filePath);

        return new RazorProjectInfo(new ProjectKey(projectKeyId), filePath, configuration, rootNamespace, displayName, projectWorkspaceState, documents);
    }
}
#endif
