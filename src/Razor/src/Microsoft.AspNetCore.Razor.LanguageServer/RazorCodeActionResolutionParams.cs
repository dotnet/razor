using System.Collections.Generic;
using MediatR;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    class RazorCodeActionResolutionParams : IRequest<RazorCodeActionResolutionResponse>
    {
        public string Action { get; set; }
        public Dictionary<string, object> Data { get; set; }
    }
}
