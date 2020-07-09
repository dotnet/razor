using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage
{
    internal class LSPConstants
    {
        /// <summary>
        /// Should be used a property name on a virtual document buffer in order for the VS Platoform LanguageServerClient
        /// infrastructure recognize it as a contained language buffer and init corresponding language server
        /// </summary>
        public const string ContainedLanguageMarker = "ContainedLanguageMarker";
    }
}
