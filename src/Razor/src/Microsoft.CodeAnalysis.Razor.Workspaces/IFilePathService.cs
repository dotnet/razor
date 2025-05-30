// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

internal interface IFilePathService
{
    string GetRazorCSharpFilePath(ProjectKey projectKey, string razorFilePath);

    DocumentUri GetRazorDocumentUri(DocumentUri virtualDocumentUri);

    bool IsVirtualCSharpFile(DocumentUri uri);

    bool IsVirtualDocumentUri(DocumentUri uri);

    bool IsVirtualHtmlFile(DocumentUri uri);
}
