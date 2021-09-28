// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage
{
    internal abstract class LSPDocumentChangeListener
    {
        public abstract void Changed(
            LSPDocumentSnapshot old,
            LSPDocumentSnapshot @new,
            VirtualDocumentSnapshot virtualOld,
            VirtualDocumentSnapshot virtualNew,
            LSPDocumentChangeKind kind);
    }
}
