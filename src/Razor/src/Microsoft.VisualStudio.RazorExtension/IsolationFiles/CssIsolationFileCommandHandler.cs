// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.VisualStudio.RazorExtension.IsolationFiles;

/// <summary>
/// Command handler for CSS isolation files (.razor.css).
/// </summary>
internal sealed class CssIsolationFileCommandHandler(IServiceProvider serviceProvider)
    : IsolationFileCommandHandler(serviceProvider, ".css")
{
    protected override string AddText => Resources.Add_CSS_Isolation_File;

    protected override string ViewText => Resources.View_CSS_Isolation_File;

    protected override string GenerateFileContent(string razorFilePath, string componentOrViewName)
    {
        var fileType = FileKinds.GetFileKindFromPath(razorFilePath).IsComponent() ? "component" : "view";

        // For CSS isolation files, we create an empty CSS file with a comment
        return $$"""
            /* Scoped CSS styles for {{componentOrViewName}} {{fileType}} */

            """;
    }
}
