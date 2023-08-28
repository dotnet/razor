// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic;

internal record RazorRange
{
    public int StartLine { get; init; }

    public int StartCharacter { get; init; }

    public int EndLine { get; init; }

    public int EndCharacter { get; init; }
}
