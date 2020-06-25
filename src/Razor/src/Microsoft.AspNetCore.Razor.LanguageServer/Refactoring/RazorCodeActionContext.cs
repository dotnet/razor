using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;


namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    struct RazorCodeActionContext
    {
        public readonly CodeActionParams Request;
        public readonly DocumentSnapshot DocumentSnapshot;
        public readonly RazorCodeDocument CodeDocument;
        public readonly SourceLocation Location;

        public RazorCodeActionContext(CodeActionParams request, DocumentSnapshot documentSnapshot, RazorCodeDocument codeDocument, SourceLocation location)
        {
            Request = request;
            DocumentSnapshot = documentSnapshot;
            CodeDocument = codeDocument;
            Location = location;
        }
    }
}
