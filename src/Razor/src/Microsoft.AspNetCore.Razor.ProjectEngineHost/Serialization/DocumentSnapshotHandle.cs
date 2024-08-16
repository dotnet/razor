// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Utilities;

namespace Microsoft.AspNetCore.Razor.Serialization;

internal record DocumentSnapshotHandle(string FilePath, string TargetPath, string FileKind)
{
    internal void CalculateChecksum(Checksum.Builder builder)
    {

        builder.AppendData(FilePath);
        builder.AppendData(TargetPath);
        builder.AppendData(FileKind);
    }
}
