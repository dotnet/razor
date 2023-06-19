// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

internal abstract class ProjectResolver
{
    public abstract bool TryResolveProject(string documentFilePath, [NotNullWhen(true)] out IProjectSnapshot? projectSnapshot);
    public abstract bool TryResolve(string normalizedPath, [NotNullWhen(true)]  out IProjectSnapshot? project, [NotNullWhen(true)] out IDocumentSnapshot? document);

    public abstract IProjectSnapshot GetMiscellaneousProject();
}
