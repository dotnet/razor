// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis.Razor.SemanticTokens;

namespace Microsoft.CodeAnalysis.Remote.Razor.SemanticTokens;

[Export(typeof(ISemanticTokensLegendService)), Shared]
internal sealed class RemoteSemanticTokensLegendService : ISemanticTokensLegendService
{
    public SemanticTokenModifiers TokenModifiers { get; private set; } = null!;

    public SemanticTokenTypes TokenTypes { get; private set; } = null!;

    // TODO: This is dodgy! Need some way to initialize data
    internal void Set(string[] tokenTypes, string[] tokenModifiers)
    {
        if (TokenTypes is null)
        {
            TokenTypes = new SemanticTokenTypes(tokenTypes);
            TokenModifiers = new SemanticTokenModifiers(tokenModifiers);
        }
    }
}
