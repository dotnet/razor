// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage
{
    internal class TestVirtualDocument : VirtualDocumentBase<TestVirtualDocumentSnapshot>
    {
        public TestVirtualDocument(Uri uri, ITextBuffer textBuffer) : base(uri, textBuffer)
        {
        }

        protected override TestVirtualDocumentSnapshot GetUpdatedSnapshot(object state) => new(Uri, HostDocumentVersion, TextBuffer.CurrentSnapshot, state);
    }
}
