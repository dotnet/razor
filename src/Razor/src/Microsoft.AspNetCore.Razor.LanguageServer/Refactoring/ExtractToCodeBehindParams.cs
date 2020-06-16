using System;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    class ExtractToCodeBehindParams
    {
        public Uri Uri { get; set; }
        public int ExtractStart { get; set; }
        public int ExtractEnd { get; set; }
        public int RemoveStart { get; set; }
        public int RemoveEnd { get; set; }
    }
}
