// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal sealed class RazorCodeActionContext
    {
        public RazorCodeActionContext(
            RazorCodeActionParams request,
            DocumentSnapshot documentSnapshot,
            RazorCodeDocument codeDocument,
            SourceLocation location,
            SourceText sourceText,
            bool supportsFileCreation)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            DocumentSnapshot = documentSnapshot ?? throw new ArgumentNullException(nameof(documentSnapshot));
            CodeDocument = codeDocument ?? throw new ArgumentNullException(nameof(codeDocument));
            Location = location;
            SourceText = sourceText ?? throw new ArgumentNullException(nameof(sourceText));
            SupportsFileCreation = supportsFileCreation;
        }

        public RazorCodeActionParams Request { get; }
        public DocumentSnapshot DocumentSnapshot { get; }
        public RazorCodeDocument CodeDocument { get; }
        public SourceLocation Location { get; }
        public SourceText SourceText { get; }
        public bool SupportsFileCreation { get; }
    }
}
