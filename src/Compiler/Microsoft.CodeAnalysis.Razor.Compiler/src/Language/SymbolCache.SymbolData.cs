// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.CodeAnalysis.Razor.Compiler.Language;

internal partial class SymbolCache
{
    public sealed partial class SymbolData(ISymbol symbol)
    {
        private readonly ISymbol _symbol = symbol;

        private ToDisplayStringResult? _toDisplayStringResult;

        public string ToDisplayString(SymbolDisplayFormat? format)
        {
            _toDisplayStringResult ??= new ToDisplayStringResult(_symbol);

            return _toDisplayStringResult.ToDisplayString(format);
        }
    }
}
