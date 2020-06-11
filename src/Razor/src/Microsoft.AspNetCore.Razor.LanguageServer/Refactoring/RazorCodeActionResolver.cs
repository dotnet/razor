using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    abstract class RazorCodeActionResolver
    {
        abstract public string Action { get; }

        abstract public Task<WorkspaceEdit> Resolve(Dictionary<string, object> data, CancellationToken cancellationToken);
    }
}
