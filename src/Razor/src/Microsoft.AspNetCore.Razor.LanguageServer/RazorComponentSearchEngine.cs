using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal abstract class RazorComponentSearchEngine
    {
        public abstract bool TryLocateComponent(TagHelperDescriptor tagHelper, out DocumentSnapshot documentSnapshot);
    }
}
