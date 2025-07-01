﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.Razor.LanguageClient;

[FileExtension(RazorLSPConstants.CSHTMLFileExtension)]
[Name(nameof(CSHTMLFilePathToContentTypeProvider))]
[Export(typeof(IFilePathToContentTypeProvider))]
[method: ImportingConstructor]
internal class CSHTMLFilePathToContentTypeProvider(
    IContentTypeRegistryService contentTypeRegistryService,
    ILspEditorFeatureDetector lspEditorFeatureDetector,
    LanguageServerFeatureOptions options)
    : RazorFilePathToContentTypeProviderBase(contentTypeRegistryService, lspEditorFeatureDetector, options)
{
}
