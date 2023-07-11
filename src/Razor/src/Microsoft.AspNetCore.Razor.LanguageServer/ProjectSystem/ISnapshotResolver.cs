// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
internal interface ISnapshotResolver
{
    /// <summary>
    /// Finds all the projects with a directory that contains the document path. 
    /// </summary>
    /// <param name="documentFilePath"></param>
    /// <param name="includeMiscellaneous">if true, will include the <see cref="GetMiscellaneousProject"/> in the results</param>
    IEnumerable<IProjectSnapshot> FindPotentialProjects(string documentFilePath, bool includeMiscellaneous);
    IProjectSnapshot GetMiscellaneousProject();

    /// <summary>
    /// Resolves a document and containing project given a document path
    /// </summary>
    /// <returns><see langword="true"/> if a document is found and contained in a project</returns>
    bool TryResolve(string documentFilePath, bool includeMiscellaneous, [NotNullWhen(true)] out IProjectSnapshot? projectSnapshot, [NotNullWhen(true)] out IDocumentSnapshot? documentSnapshot);

    bool TryResolveDocument(string documentFilePath, bool includeMiscellaneous, [NotNullWhen(true)] out IDocumentSnapshot? documentSnapshot);

    bool TryResolveProject(string documentFilePath, bool includeMiscellaneous, [NotNullWhen(true)] out IProjectSnapshot? projectSnapshot);
}
