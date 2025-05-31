// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.CodeAnalysis.Remote.Razor;

[Export(typeof(IFilePathService)), Shared]
[method: ImportingConstructor]
internal sealed class VSCodeFilePathService(LanguageServerFeatureOptions options) : AbstractFilePathService(options)
{
    private readonly LanguageServerFeatureOptions _options = options;

    public override DocumentUri GetRazorDocumentUri(DocumentUri virtualDocumentUri)
    {
        if (_options.UseRazorCohostServer && IsVirtualCSharpFile(virtualDocumentUri))
        {
            throw new InvalidOperationException("Can not get a Razor document from a generated document Uri in cohosting");
        }

        var virtualUri = virtualDocumentUri.GetRequiredParsedUri();
        if (virtualUri.Scheme == "razor-html")
        {
            var builder = new UriBuilder(virtualUri);
            builder.Scheme = Uri.UriSchemeFile;
            return base.GetRazorDocumentUri(new DocumentUri(builder.Uri));
        }

        return base.GetRazorDocumentUri(virtualDocumentUri);
    }

    public override bool IsVirtualCSharpFile(DocumentUri uri)
    {
        if (_options.UseRazorCohostServer)
        {
            return RazorUri.IsGeneratedDocumentUri(uri.GetRequiredParsedUri());
        }

        return base.IsVirtualCSharpFile(uri);
    }
}
