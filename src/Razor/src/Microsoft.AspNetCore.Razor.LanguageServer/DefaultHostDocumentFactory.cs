// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultHostDocumentFactory : HostDocumentFactory
    {
        public override HostDocument Create(string filePath, string targetFilePath)
        {
            var hostDocument = new HostDocument(filePath, targetFilePath);
            return hostDocument;
        }

        public override HostDocument Create(string filePath, string targetFilePath, string fileKind)
        {
            var hostDocument = new HostDocument(filePath, targetFilePath, fileKind);
            return hostDocument;
        }
    }
}
