// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Composition;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.Editor.Razor
{
    /// <summary>
    /// This class is VisualStudio for Mac specific to enable creating an empty document info without having IVT access to Roslyn's types.
    /// </summary>
    [Shared]
    [Export(typeof(VisualStudioMacDocumentInfoFactory))]
    internal class DefaultVisualStudioMacDocumentInfoFactory : VisualStudioMacDocumentInfoFactory
    {
        public override DocumentInfo CreateEmpty(string razorFilePath, ProjectId projectId)
        {
            var filename = razorFilePath + ".g.cs";
            var textLoader = new EmptyTextLoader(filename);
            var docId = DocumentId.CreateNewId(projectId, debugName: filename);
            return DocumentInfo.Create(
                id: docId,
                name: Path.GetFileName(filename),
                folders: Array.Empty<string>(),
                sourceCodeKind: SourceCodeKind.Regular,
                filePath: filename,
                loader: textLoader,
                isGenerated: true);
        }
    }
}
