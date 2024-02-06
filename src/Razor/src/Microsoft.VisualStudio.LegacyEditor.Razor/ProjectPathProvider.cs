// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.LegacyEditor.Razor;

[Export(typeof(IProjectPathProvider))]
[method: ImportingConstructor]
internal sealed class ProjectPathProvider(
    ITextBufferProjectService projectService,
    [Import(AllowDefault = true)] ILiveShareProjectPathProvider? liveShareProjectPathProvider) : IProjectPathProvider
{
    private readonly ITextBufferProjectService _projectService = projectService;
    private readonly ILiveShareProjectPathProvider? _liveShareProjectPathProvider = liveShareProjectPathProvider;

    public bool TryGetProjectPath(ITextBuffer textBuffer, [NotNullWhen(returnValue: true)] out string? filePath)
    {
        if (textBuffer is null)
        {
            throw new ArgumentNullException(nameof(textBuffer));
        }

        if (_liveShareProjectPathProvider is not null &&
            _liveShareProjectPathProvider.TryGetProjectPath(textBuffer, out filePath))
        {
            return true;
        }

        var project = _projectService.GetHostProject(textBuffer);
        if (project is null)
        {
            filePath = null;
            return false;
        }

        filePath = _projectService.GetProjectPath(project);
        return true;
    }
}
