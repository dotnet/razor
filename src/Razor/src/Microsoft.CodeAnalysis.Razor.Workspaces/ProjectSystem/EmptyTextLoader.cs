﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal class EmptyTextLoader : TextLoader
{
    private readonly string _filePath;
    private readonly VersionStamp _version;

    public EmptyTextLoader(string filePath)
    {
        _filePath = filePath;
        _version = VersionStamp.Create(); // Version will never change so this can be reused.
    }

    public override Task<TextAndVersion> LoadTextAndVersionAsync(LoadTextOptions options, CancellationToken cancellationToken)
    {
        // Providing an encoding here is important for debuggability. Without this edit-and-continue
        // won't work for projects with Razor files.
        return Task.FromResult(TextAndVersion.Create(SourceText.From("", Encoding.UTF8), _version, _filePath));
    }
}
