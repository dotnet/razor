using MediatR;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    class RazorCodeActionComputationParams : IRequest<RazorCodeActionComputationResponse>
    {
        public string Action { get; set; }
        public object[] Arguments { get; set; }
    }
}
