// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.IsolationFiles;
using Microsoft.VisualStudio.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.RazorExtension.IsolationFiles;

/// <summary>
/// Command handler for JavaScript isolation files (.razor.js).
/// </summary>
internal sealed class JavascriptIsolationFileCommandHandler(
    IServiceProvider serviceProvider,
    Lazy<LSPRequestInvokerWrapper> requestInvoker)
    : IsolationFileCommandHandler(serviceProvider, ".js", IsolationFileKind.JavaScript, requestInvoker)
{
    protected override string AddText => Resources.Add_JavaScript_Isolation_File;

    protected override string ViewText => Resources.View_JavaScript_Isolation_File;

    // JS isolation files are only applicable to Razor components (.razor), not MVC views (.cshtml).
    protected override bool IsApplicable(string razorFilePath)
        => FileKinds.TryGetFileKindFromPath(razorFilePath, out _);
}
