// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    internal static class LSPDocumentExtensions
    {
        public static bool TryGetVirtualCSharpDocument(this LSPDocument hostDocument, out VirtualDocument virtualDocument)
        {
            if (hostDocument is null)
            {
                throw new ArgumentNullException(nameof(hostDocument));
            }

            for (var i = 0; i < hostDocument.VirtualDocuments.Count; i++)
            {
                if (hostDocument.VirtualDocuments[i] is CSharpVirtualDocument csharpDocument)
                {
                    virtualDocument = csharpDocument;
                    return true;
                }
            }

            virtualDocument = null;
            return false;
        }

        public static bool TryGetVirtualHTMLDocument(this LSPDocument hostDocument, out VirtualDocument virtualDocument)
        {
            if (hostDocument is null)
            {
                throw new ArgumentNullException(nameof(hostDocument));
            }

            for (var i = 0; i < hostDocument.VirtualDocuments.Count; i++)
            {
                if (hostDocument.VirtualDocuments[i] is HtmlVirtualDocument htmlDocument)
                {
                    virtualDocument = htmlDocument;
                    return true;
                }
            }

            virtualDocument = null;
            return false;
        }
    }
}
