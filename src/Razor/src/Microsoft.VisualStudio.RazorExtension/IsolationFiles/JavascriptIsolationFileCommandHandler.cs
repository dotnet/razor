// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.VisualStudio.RazorExtension.IsolationFiles;

/// <summary>
/// Command handler for JavaScript isolation files (.razor.js).
/// </summary>
internal sealed class JavascriptIsolationFileCommandHandler(IServiceProvider serviceProvider)
    : IsolationFileCommandHandler(serviceProvider, ".js")
{
    protected override string AddText => Resources.Add_JavaScript_Isolation_File;

    protected override string ViewText => Resources.View_JavaScript_Isolation_File;

    // JS isolation files are only applicable to Razor components (.razor), not MVC views (.cshtml).
    protected override bool IsApplicable(string razorFilePath)
        => IsRazorComponentFile(razorFilePath);

    protected override string GenerateFileContent(string razorFilePath, string componentOrViewName)
    {
        // For JavaScript isolation files, we create an empty js file with a comment
        return $$"""
            // JavaScript isolation for {{componentOrViewName}} component
            """;
    }
}
