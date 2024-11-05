// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Mapping;

internal class RangComparer : IComparer<Range>
{
    public static readonly RangComparer Instance = new();

    public int Compare(Range? x, Range? y)
    {
        if (x is null)
        {
            return y is null ? 0 : 1;
        }

        if (y is null)
        {
            return -1;
        }

        if (x.Start.Line < y.Start.Line)
        {
            return -1;
        }

        if (x.Start.Line > y.Start.Line)
        {
            return 1;
        }

        return x.Start.Character - y.Start.Character;
    }
}
