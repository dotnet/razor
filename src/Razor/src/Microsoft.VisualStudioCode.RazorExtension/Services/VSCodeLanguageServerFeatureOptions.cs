// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudioCode.RazorExtension.Services;

// TODO:

[Shared]
[Export(typeof(LanguageServerFeatureOptions))]
internal class VSCodeLanguageServerFeatureOptions : LanguageServerFeatureOptions
{
    public override bool SupportsFileManipulation => false;

    public override string CSharpVirtualDocumentSuffix => throw new InvalidOperationException("This property is not valid in OOP");

    public override string HtmlVirtualDocumentSuffix => "*.html";

    public override bool SingleServerSupport => throw new InvalidOperationException("This option has not been synced to OOP.");

    public override bool DelegateToCSharpOnDiagnosticPublish => throw new InvalidOperationException("This option has not been synced to OOP.");

    public override bool UsePreciseSemanticTokenRanges => false;

    public override bool ShowAllCSharpCodeActions => false;

    public override bool UpdateBuffersForClosedDocuments => throw new InvalidOperationException("This option has not been synced to OOP.");

    public override bool ReturnCodeActionAndRenamePathsWithPrefixedSlash => PlatformInformation.IsWindows;

    public override bool IncludeProjectKeyInGeneratedFilePath => throw new InvalidOperationException("This option does not apply in cohosting.");

    public override bool UseRazorCohostServer => true;

    public override bool ForceRuntimeCodeGeneration => true;

    public override bool UseNewFormattingEngine => true;

    public override bool SupportsSoftSelectionInCompletion => false;

    public override bool UseVsCodeCompletionTriggerCharacters => true;

    public override bool DoNotInitializeMiscFilesProjectFromWorkspace => throw new NotImplementedException("This option has not been synced to OOP.");
}
