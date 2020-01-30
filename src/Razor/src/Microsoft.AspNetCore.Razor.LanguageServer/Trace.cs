using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    // We need to keep this in sync with the client definition, Trace.ts
    internal enum Trace
    {
        Off = 0,
        Messages = 1,
        Verbose = 2,
    }
}
