// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.Razor.LanguageClient;

internal abstract class AbstractRazorFilePathToContentTypeProvider(
    IContentTypeRegistryService contentTypeRegistryService,
    ILspEditorFeatureDetector lspEditorFeatureDetector)
    : IFilePathToContentTypeProvider
{
    private readonly IContentTypeRegistryService _contentTypeRegistryService = contentTypeRegistryService;
    private readonly ILspEditorFeatureDetector _lspEditorFeatureDetector = lspEditorFeatureDetector;

    public bool TryGetContentTypeForFilePath(string filePath, [NotNullWhen(true)] out IContentType? contentType)
    {
        if (_lspEditorFeatureDetector.IsLspEditorAvailable())
        {
            contentType = _contentTypeRegistryService.GetContentType(RazorConstants.RazorLSPContentTypeName);
            return true;
        }

        contentType = null;
        return false;
    }
}
