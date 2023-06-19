// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

internal class DefaultDocumentResolver : DocumentResolver
{
    private readonly ProjectResolver _projectResolver;

    public DefaultDocumentResolver(ProjectResolver projectResolver)
    {
        _projectResolver = projectResolver ?? throw new ArgumentNullException(nameof(projectResolver));
    }

    public override bool TryResolveDocument(string documentFilePath, [NotNullWhen(true)] out IDocumentSnapshot? document)
    {
        var normalizedPath = FilePathNormalizer.Normalize(documentFilePath);
        return _projectResolver.TryResolve(normalizedPath, out var _, out document);
    }
}
