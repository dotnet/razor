// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.Razor.LanguageClient;

internal abstract class RazorFilePathToContentTypeProviderBase : IFilePathToContentTypeProvider
{
    private readonly IContentTypeRegistryService _contentTypeRegistryService;
    private readonly ILspEditorFeatureDetector _lspEditorFeatureDetector;
    private readonly LanguageServerFeatureOptions _options;

    public RazorFilePathToContentTypeProviderBase(
        IContentTypeRegistryService contentTypeRegistryService,
        ILspEditorFeatureDetector lspEditorFeatureDetector,
        LanguageServerFeatureOptions options)
    {
        _contentTypeRegistryService = contentTypeRegistryService;
        _lspEditorFeatureDetector = lspEditorFeatureDetector;
        _options = options;
    }

    public bool TryGetContentTypeForFilePath(string filePath, [NotNullWhen(true)] out IContentType? contentType)
    {
        // When cohosting is on, it's on for all non .NET Framework projects, regardless of feature flags or
        // project capabilities.
        if (_options.UseRazorCohostServer &&
            _lspEditorFeatureDetector.IsDotNetCoreProject(filePath))
        {
            contentType = _contentTypeRegistryService.GetContentType(RazorConstants.RazorLSPContentTypeName);
            return true;
        }

        if (_lspEditorFeatureDetector.IsLspEditorEnabled() &&
            _lspEditorFeatureDetector.IsLspEditorSupported(filePath))
        {
            contentType = _contentTypeRegistryService.GetContentType(RazorConstants.RazorLSPContentTypeName);
            return true;
        }

        contentType = null;
        return false;
    }
}
