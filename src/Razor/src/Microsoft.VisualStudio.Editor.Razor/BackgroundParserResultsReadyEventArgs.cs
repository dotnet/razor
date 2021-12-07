// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.Language;
using static Microsoft.VisualStudio.Editor.Razor.BackgroundParser;

namespace Microsoft.VisualStudio.Editor.Razor
{
    internal class BackgroundParserResultsReadyEventArgs : EventArgs
    {
        public BackgroundParserResultsReadyEventArgs(ChangeReference edit, RazorCodeDocument codeDocument)
        {
            ChangeReference = edit;
            CodeDocument = codeDocument;
        }

        public ChangeReference ChangeReference { get; }

        public RazorCodeDocument CodeDocument { get; }
    }
}
