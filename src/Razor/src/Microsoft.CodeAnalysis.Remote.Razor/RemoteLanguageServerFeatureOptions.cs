// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.CodeAnalysis.Remote.Razor;

// TODO: Initialize this properly
[Export(typeof(LanguageServerFeatureOptions)), Shared]
internal sealed class RemoteLanguageServerFeatureOptions : LanguageServerFeatureOptions
{
    public override bool SupportsFileManipulation => true;

    public override string ProjectConfigurationFileName => "project.razor.vs.json";

    public override string CSharpVirtualDocumentSuffix => ".ide.g.cs";

    public override string HtmlVirtualDocumentSuffix => "__virtual.html";

    public override bool SingleServerCompletionSupport => false;

    public override bool SingleServerSupport => false;

    public override bool DelegateToCSharpOnDiagnosticPublish => false;

    public override bool UpdateBuffersForClosedDocuments => false;

    public override bool ReturnCodeActionAndRenamePathsWithPrefixedSlash => true;

    public override bool ShowAllCSharpCodeActions => false;

    public override bool IncludeProjectKeyInGeneratedFilePath => false;

    public override bool UsePreciseSemanticTokenRanges => false;

    public override bool MonitorWorkspaceFolderForConfigurationFiles => true;

    public override bool UseRazorCohostServer => true;

    public override bool DisableRazorLanguageServer => false;
}
