// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Roslyn.LanguageServer.Protocol;

internal static partial class LspExtensions
{
    public static void Deconstruct(this LspLocation position, out DocumentUri uri, out LspRange range)
        => (uri, range) = (position.DocumentUri, position.Range);
}
