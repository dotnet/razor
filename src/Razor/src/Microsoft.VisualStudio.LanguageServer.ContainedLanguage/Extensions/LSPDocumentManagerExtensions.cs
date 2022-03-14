// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage.Extensions
{
    internal static class LSPDocumentManagerExtensions
    {
        public static bool TryGetDocument(this LSPDocumentManager documentManager!!, string filePath!!, out LSPDocumentSnapshot lspDocumentSnapshot)
        {
            if (filePath.StartsWith("/", StringComparison.Ordinal) && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                filePath = filePath.Substring(1);
            }

            var uri = new Uri(filePath, UriKind.Absolute);
            return documentManager.TryGetDocument(uri, out lspDocumentSnapshot);
        }
    }
}
