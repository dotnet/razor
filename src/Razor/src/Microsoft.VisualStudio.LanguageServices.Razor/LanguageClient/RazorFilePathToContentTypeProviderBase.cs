﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.Razor.LanguageClient;

internal abstract class RazorFilePathToContentTypeProviderBase(
    IContentTypeRegistryService contentTypeRegistryService,
    ILspEditorFeatureDetector lspEditorFeatureDetector,
    LanguageServerFeatureOptions options) : IFilePathToContentTypeProvider
{
    private readonly IContentTypeRegistryService _contentTypeRegistryService = contentTypeRegistryService;
    private readonly ILspEditorFeatureDetector _lspEditorFeatureDetector = lspEditorFeatureDetector;
    private readonly LanguageServerFeatureOptions _options = options;

    public bool TryGetContentTypeForFilePath(string filePath, [NotNullWhen(true)] out IContentType? contentType)
    {
        if (UseLSPEditor(filePath))
        {
            contentType = _contentTypeRegistryService.GetContentType(RazorConstants.RazorLSPContentTypeName);
            return true;
        }

        contentType = null;
        return false;
    }

    private bool UseLSPEditor(string filePath)
    {
        // When cohosting is on, it's on for all non .NET Framework projects, regardless of feature flags or
        // project capabilities.
        if (_options.UseRazorCohostServer &&
            _lspEditorFeatureDetector.IsDotNetCoreProject(filePath))
        {
            return true;
        }

        // Otherwise, we just check for the lack of feature flag feature or project capability.
        if (_lspEditorFeatureDetector.IsLspEditorEnabled() &&
            _lspEditorFeatureDetector.IsLspEditorSupported(filePath))
        {
            return true;
        }

        return false;
    }
}
