// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.OmniSharp.Project;

internal class OmniSharpProjectChangeEventArgs : EventArgs
{
    internal OmniSharpProjectChangeEventArgs(ProjectChangeEventArgs args) : this(
        OmniSharpProjectSnapshot.Convert(args.Older),
        OmniSharpProjectSnapshot.Convert(args.Newer),
        args.DocumentFilePath,
        (OmniSharpProjectChangeKind)args.Kind)
    {
        InternalProjectChangeEventArgs = args;
    }

    private OmniSharpProjectChangeEventArgs(OmniSharpProjectSnapshot older, OmniSharpProjectSnapshot newer, string documentFilePath, OmniSharpProjectChangeKind kind)
    {
        if (older is null && newer is null)
        {
            throw new ArgumentException("Both projects cannot be null.");
        }

        Older = older;
        Newer = newer;
        DocumentFilePath = documentFilePath;
        Kind = kind;

        ProjectFilePath = older?.FilePath ?? newer.FilePath;
    }

    internal ProjectChangeEventArgs InternalProjectChangeEventArgs { get; }

    internal OmniSharpProjectSnapshot Older { get; }

    internal OmniSharpProjectSnapshot Newer { get; }

    internal string ProjectFilePath { get; }

    internal string DocumentFilePath { get; }

    internal OmniSharpProjectChangeKind Kind { get; }

    internal static OmniSharpProjectChangeEventArgs CreateTestInstance(OmniSharpProjectSnapshot older, OmniSharpProjectSnapshot newer, string documentFilePath, OmniSharpProjectChangeKind kind) =>
        new(older, newer, documentFilePath, kind);
}
