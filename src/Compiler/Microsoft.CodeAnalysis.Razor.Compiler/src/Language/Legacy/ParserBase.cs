// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

internal abstract class ParserBase
{
    public ParserBase(ParserContext context)
    {
        Context = context;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public ParserContext Context { get; }
}
