// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.Editor.Razor;

internal interface ITextBufferProjectService
{
    object? GetHostProject(ITextBuffer textBuffer);
    object? GetHostProject(string documentFilePath);
    bool IsSupportedProject(object project);
    string GetProjectPath(object project);
    string? GetProjectName(object project);
}
