using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    internal abstract class RazorComponentSearchEngine
    {
        public abstract Task<Tuple<Uri, DocumentSnapshot, RazorCodeDocument>> TryLocateComponent(ProjectSnapshot project, string name, string fullyQualifiedName, CancellationToken cancellationToken);
    }
}
