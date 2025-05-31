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
internal sealed class RemoteFilePathService(LanguageServerFeatureOptions options) : AbstractFilePathService(options)
{
    public override DocumentUri GetRazorDocumentUri(DocumentUri virtualDocumentUri)
    {
        if (IsVirtualCSharpFile(virtualDocumentUri))
        {
            throw new InvalidOperationException("Can not get a Razor document from a generated document Uri in cohosting");
        }

        return base.GetRazorDocumentUri(virtualDocumentUri);
    }

    public override bool IsVirtualCSharpFile(DocumentUri uri)
        => RazorUri.IsGeneratedDocumentUri(uri.GetRequiredParsedUri());
}
