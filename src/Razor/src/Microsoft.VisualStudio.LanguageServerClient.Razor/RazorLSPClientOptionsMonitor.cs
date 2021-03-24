// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.Composition;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    [Export(typeof(RazorLSPClientOptionsMonitor))]
    internal class RazorLSPClientOptionsMonitor
    {
        public bool InsertSpaces { get; private set; }

        public int TabSize { get; private set; }

        public void UpdateOptions(bool insertSpaces, int tabSize)
        {
            InsertSpaces = insertSpaces;
            TabSize = tabSize;
        }
    }
}
