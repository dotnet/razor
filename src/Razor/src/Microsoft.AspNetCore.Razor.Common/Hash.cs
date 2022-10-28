// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.Common;

internal static class Hash
{
    public static int Combine(int newKey, int currentKey)
        => unchecked((currentKey * (int)0xA5555529) + newKey);

    public static int Combine(bool newKeyPart, int currentKey)
        => Combine(currentKey, newKeyPart ? 1 : 0);
}
