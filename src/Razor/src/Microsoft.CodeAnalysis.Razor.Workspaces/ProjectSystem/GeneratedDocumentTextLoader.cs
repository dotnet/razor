﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal class GeneratedDocumentTextLoader : TextLoader
{
    private readonly IDocumentSnapshot _document;
    private readonly string _filePath;
    private readonly VersionStamp _version;

    public GeneratedDocumentTextLoader(IDocumentSnapshot document, string filePath)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        _document = document;
        _filePath = filePath;
        _version = VersionStamp.Create();
    }

    public override async Task<TextAndVersion> LoadTextAndVersionAsync(LoadTextOptions options, CancellationToken cancellationToken)
    {
        var output = await _document.GetGeneratedOutputAsync().ConfigureAwait(false);

        // Providing an encoding here is important for debuggability. Without this edit-and-continue
        // won't work for projects with Razor files.
        return TextAndVersion.Create(SourceText.From(output.GetCSharpDocument().GeneratedCode, Encoding.UTF8), _version, _filePath);
    }
}
