// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.Extensions.Internal;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal sealed record class HostDocument
{
    public string FileKind { get; init; }
    public string FilePath { get; init; }
    public string TargetPath { get; init; }

    public HostDocument(string filePath, string targetPath, string? fileKind = null)
    {
        FilePath = filePath;
        TargetPath = targetPath;
        FileKind = fileKind ?? FileKinds.GetFileKindFromFilePath(filePath);
    }

    public bool Equals(HostDocument? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null &&
               FilePathComparer.Instance.Equals(FilePath, other.FilePath) &&
               FilePathComparer.Instance.Equals(TargetPath, other.TargetPath) &&
               FileKind == other.FileKind;
    }

    public override int GetHashCode()
    {
        var hash = HashCodeCombiner.Start();

        hash.Add(FilePath, FilePathComparer.Instance);
        hash.Add(TargetPath, FilePathComparer.Instance);
        hash.Add(FileKind);

        return hash.CombinedHash;
    }
}
