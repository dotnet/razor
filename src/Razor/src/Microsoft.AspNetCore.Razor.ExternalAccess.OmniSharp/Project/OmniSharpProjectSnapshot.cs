// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.AspNetCore.Razor.ExternalAccess.OmniSharp.Document;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.OmniSharp.Project;

public sealed class OmniSharpProjectSnapshot
{
    internal readonly ProjectSnapshot InternalProjectSnapshot;

    internal OmniSharpProjectSnapshot(ProjectSnapshot projectSnapshot)
    {
        InternalProjectSnapshot = projectSnapshot;
    }

    public string FilePath => InternalProjectSnapshot.FilePath;

    public IEnumerable<string> DocumentFilePaths => InternalProjectSnapshot.DocumentFilePaths;

    internal RazorConfiguration Configuration => InternalProjectSnapshot.Configuration;

    public ProjectWorkspaceState ProjectWorkspaceState => InternalProjectSnapshot.ProjectWorkspaceState;

    public OmniSharpDocumentSnapshot GetDocument(string filePath)
    {
        var documentSnapshot = InternalProjectSnapshot.GetDocument(filePath);
        if (documentSnapshot is null)
        {
            return null;
        }

        var internalDocumentSnapshot = new OmniSharpDocumentSnapshot(documentSnapshot);
        return internalDocumentSnapshot;
    }

    internal static OmniSharpProjectSnapshot Convert(ProjectSnapshot projectSnapshot)
    {
        if (projectSnapshot is null)
        {
            return null;
        }

        return new OmniSharpProjectSnapshot(projectSnapshot);
    }
}
