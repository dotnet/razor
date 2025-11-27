// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Composition;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudioCode.RazorExtension.Services;

[Shared]
[Export(typeof(LanguageServerFeatureOptions))]
[method: ImportingConstructor]
internal class VSCodeLanguageServerFeatureOptions() : LanguageServerFeatureOptions
{
    public override bool SupportsFileManipulation => true;
    public override bool ShowAllCSharpCodeActions => false;
    public override bool ReturnCodeActionAndRenamePathsWithPrefixedSlash => PlatformInformation.IsWindows;
    public override bool UseRazorCohostServer => true;

    // Options that don't apply to VS Code/Cohosting at all
    public override bool IncludeProjectKeyInGeneratedFilePath => throw new InvalidOperationException();
    public override bool SingleServerSupport => throw new InvalidOperationException();
    public override string CSharpVirtualDocumentSuffix => throw new InvalidOperationException();
}
