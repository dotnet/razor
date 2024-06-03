// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.Razor;

internal interface ILspEditorFeatureDetector
{
    /// <summary>
    /// Returns <see langword="true"/> if the LSP-based editor is available.
    /// </summary>
    bool IsLspEditorAvailable();

    /// <summary>
    /// Returns <see langword="true"/> if this is a LiveShare guest or a CodeSpaces client.
    /// </summary>
    bool IsRemoteClient();

    /// <summary>
    /// Returns <see langword="true"/> if this is a LiveShare host.
    /// </summary>
    bool IsLiveShareHost();
}
