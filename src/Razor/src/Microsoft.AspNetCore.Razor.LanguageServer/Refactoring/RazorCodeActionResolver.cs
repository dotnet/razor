using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    abstract class RazorCodeActionResolver
    {
        public abstract string Action { get; }

        public abstract Task<WorkspaceEdit> ResolveAsync(JObject data, CancellationToken cancellationToken);
    }
}
