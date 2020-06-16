using System;
using Microsoft.AspNetCore.Razor.Language;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;


namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    struct RazorCodeActionContext
    {
        public readonly CodeActionParams Request;
        public readonly Uri Uri;
        public readonly RazorCodeDocument Document;
        public readonly SourceLocation Location;

        public RazorCodeActionContext(CodeActionParams request, Uri uri, RazorCodeDocument document, SourceLocation location)
        {
            Uri = uri;
            Request = request;
            Document = document;
            Location = location;
        }
    }
}
