// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal static class Extensions
{
    public static ProjectSnapshotHandle ToHandle(this IProjectSnapshot snapshot)
        => new(snapshot.FilePath, snapshot.Configuration, snapshot.RootNamespace);
}
