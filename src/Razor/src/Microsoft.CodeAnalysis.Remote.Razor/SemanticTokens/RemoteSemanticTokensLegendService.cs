// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis.Razor.SemanticTokens;

namespace Microsoft.CodeAnalysis.Remote.Razor.SemanticTokens;

[Export(typeof(ISemanticTokensLegendService)), Shared]
internal sealed class RemoteSemanticTokensLegendService : ISemanticTokensLegendService
{
    // TODO: Get some tokens
    public SemanticTokenModifiers TokenModifiers => new SemanticTokenModifiers([]);

    public SemanticTokenTypes TokenTypes => new SemanticTokenTypes([]);
}
