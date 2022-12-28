// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

internal class MarkupChunkGenerator : SpanChunkGenerator
{
    public static readonly MarkupChunkGenerator Instance = new();

    private MarkupChunkGenerator() { }

    public override string ToString()
    {
        return "Markup";
    }
}
