// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.Language;

public static class CommonMetadata
{
    internal static KeyValuePair<string, string?> MakeTrue(string key)
        => new(key, bool.TrueString);
}
