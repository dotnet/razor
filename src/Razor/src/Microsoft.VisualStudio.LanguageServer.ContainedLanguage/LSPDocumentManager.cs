// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage
{
    public abstract class LSPDocumentManager
    {
        public abstract bool TryGetDocument(Uri uri, out LSPDocumentSnapshot lspDocumentSnapshot);
    }
}
