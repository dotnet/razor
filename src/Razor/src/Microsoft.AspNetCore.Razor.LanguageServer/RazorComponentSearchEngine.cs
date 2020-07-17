using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal abstract class RazorComponentSearchEngine
    {
        public abstract Task<Tuple<Uri, DocumentSnapshot, RazorCodeDocument>> TryLocateComponent(ProjectSnapshot project, string componentName, string fullyQualifiedComponentName, CancellationToken cancellationToken);
    }
}
