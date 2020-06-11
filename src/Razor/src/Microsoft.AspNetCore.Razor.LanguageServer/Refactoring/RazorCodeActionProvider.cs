using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    abstract class RazorCodeActionProvider
    {
        abstract public Task<CommandOrCodeActionContainer> Provide(RazorCodeActionContext context, CancellationToken cancellationToken);
    }
}
